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
//TODO: Add SeSouvenirDeMoi functionality

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
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Profil", "Compte");
            }
            return View();
        }

        // POST: /Compte/SeConnecter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeConnecter(SeConnecterViewModel model)
        {
            if (ModelState.IsValid)
            {
                _logger.LogInformation("Processing login for {Email}", model.Email);
                // Chercher l'utilisateur par email
                var utilisateur = await _context.Utilisateur.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == model.Email);
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
                                new Claim("UserId", utilisateur.Id.ToString()),
                                new Claim(ClaimTypes.Role, utilisateur.Role != null ? utilisateur.Role.Libelle.ToString() : "Observateur")
                            };

                            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                            await HttpContext.SignInAsync(
                                CookieAuthenticationDefaults.AuthenticationScheme,
                                new ClaimsPrincipal(identity));
                            _logger.LogInformation("Utilisateur {Email} logged in successfully.", model.Email);

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
            _logger.LogInformation("User {User} is logging out.", User.Identity.Name);
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("SeConnecter", "Compte");
        }

        // GET: /Compte/DemanderUnCompte
        [HttpGet]
        public async Task<IActionResult> DemanderUnCompte()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Profil", "Compte");
            }
            _logger.LogInformation("DemanderUnCompte page requested.");
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
            _logger.LogInformation("Processing account request for {Email}", model.Email);
            if (ModelState.IsValid)
            {
                // Validate password strength
                if (!IsPasswordSecure(model.MotDePasse))
                {
                    ModelState.AddModelError("MotDePasse", "Le mot de passe doit contenir au moins 8 caractères, dont une majuscule, une minuscule, un chiffre et un caractère spécial.");
                    _logger.LogWarning("Weak password provided for account request: {Email}", model.Email);
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
                    //TODO: send verification email for user before creating his request
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
                    _logger.LogInformation("Account request created for {Email}", model.Email);
                }
                else
                {
                    _logger.LogWarning("Account request already exists for {Email}", model.Email);
                }

                //TODO: Send email befor adding to tabel to user mail verification then page of mail verification then succes message
                // For now, assume you will send a verification email next.
                TempData["Success"] = "Votre demande de compte a été reçue. Veuillez vérifier votre email pour confirmation.";
                return RedirectToAction("SeConnecter");

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
        static bool IsPasswordSecure(string password)
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

        // GET: /Compte/Profil
        [HttpGet]
        public async Task<IActionResult> Profil()
        {
            var userEmail = User.Identity.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("Profile page requested without a logged in user.");
                return RedirectToAction("SeConnecter", "Compte");
            }

            var utilisateur = await _context.Utilisateur
                .Include(u => u.Entreprise)
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == userEmail);

            if (utilisateur == null)
            {
                _logger.LogWarning("User {Email} not found when accessing profile.", userEmail);
                return NotFound();
            }

            var model = new ProfilViewModel
            {
                Nom = utilisateur.Nom,
                Prenom = utilisateur.Prenom,
                Email = utilisateur.Email,
                Entreprise = utilisateur.Entreprise != null ? utilisateur.Entreprise.Nom : string.Empty,
                Role = utilisateur.Role != null ? utilisateur.Role.Libelle : "Utilisateur",
                LienLinkedIn = utilisateur.LienLinkedIn != null ? utilisateur.LienLinkedIn : "",
                DescriptionProfil = utilisateur.DescriptionProfil != null ? utilisateur.DescriptionProfil : ""
            }; 
            _logger.LogInformation("Profile page loaded for {Email}.", userEmail);

            return View(model);
        }

        // POST: /Compte/Profil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profil(ProfilViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userEmail = User.Identity.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning("Profile update attempted without a logged in user.");
                    return RedirectToAction("SeConnecter", "Compte");
                }

                var utilisateur = await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (utilisateur == null)
                {
                    _logger.LogWarning("User {Email} not found during profile update.", userEmail);
                    return NotFound();
                }

                utilisateur.Nom = model.Nom;
                utilisateur.Prenom = model.Prenom;
                utilisateur.DateModification = DateTime.Now;
                utilisateur.LienLinkedIn = model.LienLinkedIn != null ? model.LienLinkedIn : "";
                utilisateur.DescriptionProfil = model.DescriptionProfil != null ? model.DescriptionProfil : "";
                _logger.LogInformation("Profile updated successfully for {Email}.", userEmail);

                _context.Update(utilisateur);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Votre profil a été mis à jour avec succès!";
                return RedirectToAction("Profil");
            }

            _logger.LogWarning("Profile update failed due to invalid model state for user {Email}.", User.Identity.Name);
            return View("Profil", model);
        }

        // GET: /Compte/ChangerMotDePasse
        [HttpGet]
        public async Task<IActionResult> ChangerMotDePasse()
        {
            var userEmail = User.Identity.Name;
            if (string.IsNullOrEmpty(userEmail))
            {
                _logger.LogWarning("Change password page requested without a logged in user.");
                return RedirectToAction("SeConnecter", "Compte");
            }
            var utilisateur = await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == userEmail);
            if (utilisateur == null)
            {
                _logger.LogWarning("User {Email} not found when accessing change password page.", userEmail);
                return NotFound();
            }
            var model = new ChangerMotDePasseViewModel
            {
                Email = utilisateur.Email,
                MotDePasseActuel = ""
            };
            return View(model);
        }

        // POST: /Compte/ChangerMotDePasse
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangerMotDePasse(ChangerMotDePasseViewModel model)
        {
            if (ModelState.IsValid)
            {
                var userEmail = User.Identity.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    _logger.LogWarning("Change password attempted without a logged in user.");
                    return RedirectToAction("SeConnecter", "Compte");
                }

                var utilisateur = await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == userEmail);
                if (utilisateur == null)
                {
                    _logger.LogWarning("User {Email} not found during change password process.", userEmail);
                    return NotFound();
                }

                var result = _passwordHasher.VerifyHashedPassword(utilisateur, utilisateur.MotDePasseHash, model.MotDePasseActuel);
                if (result != PasswordVerificationResult.Success)
                {
                    ModelState.AddModelError("MotDePasseActuel", "Le mot de passe actuel est incorrect.");
                    _logger.LogWarning("Incorrect current password provided by user {Email} during change password.", userEmail);
                    return View(model);
                }

                if (!IsPasswordSecure(model.NouveauMotDePasse))
                {
                    ModelState.AddModelError("NouveauMotDePasse", "Le nouveau mot de passe n'est pas suffisamment sécurisé. Il doit contenir au moins 8 caractères, une majuscule, une minuscule, un chiffre et un caractère spécial.");
                    _logger.LogWarning("Insecure new password provided by user {Email}.", userEmail);
                    return View(model);
                }
                utilisateur.MotDePasseHash = _passwordHasher.HashPassword(utilisateur, model.NouveauMotDePasse);
                utilisateur.DateModification = DateTime.Now;
                _context.Update(utilisateur);
                await _context.SaveChangesAsync();

                //TODO: Send email to user for password change Notification
                _logger.LogInformation("Password updated successfully for {Email}.", userEmail);
                TempData["SuccessMessage"] = "Votre mot de passe a été mis à jour avec succès!";
                return RedirectToAction("Profil");
            }

            _logger.LogWarning("Change password failed due to invalid model state for user {Email}.", User.Identity.Name);
            return View(model);
        }

        // GET: /Compte/MotDePasseOublie
        [HttpGet]
        public IActionResult MotDePasseOublie()
        {
            return View();
        }

        // POST: /Compte/MotDePasseOublie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MotDePasseOublie(MotDePasseOublieViewModel model)
        {
            //TOD: Send email to user for password reset
            ModelState.AddModelError(string.Empty, "Vous devriez recevoir sous peu un courriel vous permettant de réinitialiser votre mot de passe.");
            return View(model);
        }
    }
}