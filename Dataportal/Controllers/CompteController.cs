using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

//TODO: Add recaptcha for login and account request forms

namespace Dataportal.Controllers
{
    public class CompteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Utilisateur> _passwordHasher;
        private readonly ILogger<CompteController> _logger;

        // Configuration pour le verrouillage du compte
        private const int MaxFailedAccessAttempts = 10;
        private readonly TimeSpan LockoutTimeSpan = TimeSpan.FromMinutes(15);

        public CompteController(ApplicationDbContext context, IPasswordHasher<Utilisateur> passwordHasher, ILogger<CompteController> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        // GET: /Compte/SeConnecter
        [HttpGet]
        public IActionResult SeConnecter()
        {
            return View();
        }

        // POST: /Compte/SeConnecter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeConnecter(SeConnecterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Chercher l'utilisateur par email
                var utilisateur = await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == model.Email);
                if (utilisateur != null)
                {
                    // Vérifier si le compte est déjà verrouillé
                    if (utilisateur.FinLockout.HasValue && utilisateur.FinLockout.Value > DateTime.UtcNow)
                    {
                        ModelState.AddModelError(string.Empty, $"Votre compte est verrouillé jusqu'à {utilisateur.FinLockout.Value.ToLocalTime()}.");
                        _logger.LogWarning("Tentative de connexion sur un compte verrouillé: {Email}", model.Email);
                        return View(model);
                    }

                    if (utilisateur.CompteActif)
                    {
                        // Vérifier le mot de passe haché
                        var verificationResult = _passwordHasher.VerifyHashedPassword(utilisateur, utilisateur.MotDePasseHash, model.MotDePasse);
                        if (verificationResult == PasswordVerificationResult.Success)
                        {
                            // Réinitialiser le compteur d'échecs et le verrouillage éventuel
                            utilisateur.NbrEchecsAcces = 0;
                            utilisateur.FinLockout = null;

                            // TODO: MFA
                            // Placez ici la logique d'authentification multifactorielle (MFA) si activée
                            // if (utilisateur.EstMfaActive)
                            // {
                            //     // Implémentez ici l'envoi du code MFA et redirigez vers une page de vérification
                            //     // Exemple: return RedirectToAction("VerifMfa", new { email = utilisateur.Email });
                            // }

                            // Mettre à jour la date du dernier login
                            utilisateur.DernierLogin = DateTime.Now;
                            await _context.SaveChangesAsync();

                            // Créer des claims pour l'utilisateur
                            var claims = new List<Claim>
                            {
                                new Claim(ClaimTypes.Name, utilisateur.Email),
                                new Claim("NomComplet", $"{utilisateur.Prenom} {utilisateur.Nom}"),
                                new Claim(ClaimTypes.Role, utilisateur.Role != null ? utilisateur.Role.ToString() : "Utilisateur")
                            };

                            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                            await HttpContext.SignInAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme,
                                new ClaimsPrincipal(identity));

                            return RedirectToAction("Index", "Accueil");
                        }
                        else
                        {
                            // Incrémenter le compteur d'échecs de connexion
                            utilisateur.NbrEchecsAcces++;
                            _logger.LogWarning("Tentative de connexion échouée pour {Email}. Nombre d'échecs: {Count}", model.Email, utilisateur.NbrEchecsAcces);

                            // Si le nombre d'échecs dépasse le seuil, verrouiller le compte
                            if (utilisateur.NbrEchecsAcces >= MaxFailedAccessAttempts)
                            {
                                utilisateur.FinLockout = DateTime.UtcNow.Add(LockoutTimeSpan);
                                _logger.LogWarning("Compte verrouillé pour {Email} jusqu'à {LockoutEnd}", model.Email, utilisateur.FinLockout.Value);
                                ModelState.AddModelError(string.Empty, $"Votre compte a été verrouillé en raison de trop nombreuses tentatives infructueuses. Veuillez réessayer après {utilisateur.FinLockout.Value.ToLocalTime()}.");
                            }

                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Le compte n'est pas actif.");
                        _logger.LogWarning("Tentative de connexion sur un compte inactif: {Email}", model.Email);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Tentative de connexion invalide.");
                    _logger.LogWarning("Tentative de connexion pour un email non existant: {Email}", model.Email);
                }
            }
            return View(model);
        }

        // POST: /Compte/SeDeconnecter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeDeconnecter()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Accueil");
        }

        // GET: /Compte/DemanderUnCompte
        [HttpGet]
        public async Task<IActionResult> DemanderUnCompte()
        {
            var viewModel = new DemanderUnCompteViewModel
            {
                Entreprises = await _context.Entreprise
                    .Where(e => e.Actif)
                    .Select(e => new SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = e.Nom
                    }).ToListAsync()
            };

            return View(viewModel);
        }

        // POST: /Compte/DemanderUnCompte
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DemanderUnCompte(DemanderUnCompteViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Validate password strength
                if (!IsPasswordSecure(model.MotDePasse))
                {
                    ModelState.AddModelError("MotDePasse", "Le mot de passe doit contenir au moins 8 caractères, dont une majuscule, une minuscule, un chiffre et un caractère spécial.");
                }

                // Retrieve the selected enterprise with its email domains.
                var entreprise = await _context.Entreprise
                    .Include(e => e.DomaineEmails)
                    .FirstOrDefaultAsync(e => e.Id == model.IdEntreprise);

                if (entreprise == null)
                {
                    ModelState.AddModelError("", "L'Établissement est requis.");
                }
                else
                {
                    // Extract the domain from the user's email.
                    var emailDomain = model.Email.Split('@').Last();

                    // Check if the selected enterprise supports this email domain.
                    bool domainValid = entreprise.DomaineEmails.Any(d =>
                        d.Domaine.Equals(emailDomain, StringComparison.OrdinalIgnoreCase) &&
                        d.DomaineActif);

                    if (!domainValid)
                    {
                        ModelState.AddModelError("Email", "Le domaine de l'email ne correspond pas à celui de l'établissement sélectionnée.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    // Reload the entreprises for the dropdown before returning the view.
                    model.Entreprises = await _context.Entreprise
                        .Where(e => e.Actif)
                        .Select(e => new SelectListItem
                        {
                            Value = e.Id.ToString(),
                            Text = e.Nom
                        }).ToListAsync();

                    return View(model);
                }

                // Assume that the request is created with the "En Attente" status.
                int statutEnAttenteId = StatutDeLaDemandeIds.EnAttente;

                // Hash the password (here we use _passwordHasher similar to login; adjust as needed)
                // Note: you might want to use a dedicated hasher for account requests.
                var hashedPassword = _passwordHasher.HashPassword(null, model.MotDePasse);

                var existingRequest = await _context.DemandeDeCompte.FirstOrDefaultAsync(d => d.Email.ToLower() == model.Email.ToLower() && d.IdEntreprise == model.IdEntreprise);
                if (existingRequest == null)
                {
                    var demande = new DemandeDeCompte
                    {
                        Nom = model.Nom,
                        Prenom = model.Prenom,
                        Email = model.Email,
                        MotDePasseHash = hashedPassword,
                        IdEntreprise = model.IdEntreprise,
                        IdStatutDeLaDemande = statutEnAttenteId,
                        EmailVerifie = false,
                        Commentaire = model.Commentaire,
                        DateCreation = DateTime.Now
                    };
                    _context.DemandeDeCompte.Add(demande);
                    await _context.SaveChangesAsync();
                }

                //TODO: Send email befor adding to tabel to user mail verification then page of mail verification then succes message
                // For now, assume you will send a verification email next.
                TempData["Message"] = "Votre demande de compte a été reçue. Veuillez vérifier votre email pour confirmation.";
                return RedirectToAction("DemanderUnCompte");

                //TODO: Send email to admin for approval
            }

            // If model state is invalid, reload the entreprises dropdown.
            model.Entreprises = await _context.Entreprise
                .Where(e => e.Actif)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Nom
                }).ToListAsync();

            return View(model);
        }

        // Helper method to check password strength.
        private bool IsPasswordSecure(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                return false;
            if (!password.Any(char.IsUpper))
                return false;
            if (!password.Any(char.IsLower))
                return false;
            if (!password.Any(char.IsDigit))
                return false;
            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                return false;
            return true;
        }
    }
}