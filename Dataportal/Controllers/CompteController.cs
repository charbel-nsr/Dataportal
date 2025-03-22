using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dataportal.Controllers
{
    public class CompteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Utilisateur> _passwordHasher;
        private readonly ILogger<CompteController> _logger;

        // Configuration pour le verrouillage du compte
        private const int MaxFailedAccessAttempts = 5;
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
    }
}