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
using Dataportal.Services.Email;
using Microsoft.Extensions.Options;

//TODO: Add recaptcha for login and account request forms

namespace Dataportal.Controllers
{
    public class CompteController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPasswordHasher<Utilisateur> _passwordHasher;
        private readonly ILogger<CompteController> _logger;
        private readonly IAccountEmailService _accountEmailService;
        private readonly PortalOptions _portalOptions;

        // Configuration pour le verrouillage du compte
        private const int MaxFailedAccessAttempts = 10;
        private readonly TimeSpan LockoutTimeSpan = TimeSpan.FromMinutes(15);

        public CompteController(ApplicationDbContext context, IPasswordHasher<Utilisateur> passwordHasher, ILogger<CompteController> logger, IAccountEmailService accountEmailService, IOptions<PortalOptions> portalOptions)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
            _accountEmailService = accountEmailService;
            _portalOptions = portalOptions.Value;
        }

        // GET: /Compte/SeConnecter
        [HttpGet]
        public IActionResult SeConnecter(string? returnUrl = null)
        {
            if (User.Identity.IsAuthenticated)
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Profil", "Compte");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Compte/SeConnecter
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SeConnecter(SeConnecterViewModel model, string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(returnUrl) && !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = null;
            }

            ViewData["ReturnUrl"] = returnUrl;

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
                        ModelState.AddModelError(string.Empty, $"Your account is locked until {utilisateur.FinLockout.Value.ToLocalTime()}.");
                        _logger.LogWarning("Login attempt on a locked account: {Email}", model.Email);
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

                            if (utilisateur.MfaEnabled)
                            {
                                var code = SecurityTokenHelper.GenerateNumericCode();
                                utilisateur.MfaCodeHash = SecurityTokenHelper.ComputeSha256(code);
                                utilisateur.MfaCodeExpiration = DateTime.UtcNow.AddMinutes(10);
                                await _context.SaveChangesAsync();

                                await _accountEmailService.SendMfaCodeAsync(utilisateur, code);

                                HttpContext.Session.SetString("PendingMfaEmail", utilisateur.Email);
                                HttpContext.Session.SetString("PendingMfaReturnUrl", returnUrl ?? string.Empty);
                                HttpContext.Session.SetString("PendingMfaRememberMe", model.SeSouvenirDeMoi.ToString());
                                TempData["Success"] = "A verification code has been sent to your email.";
                                return RedirectToAction("VerifierMfa");
                            }

                            utilisateur.DernierLogin = DateTime.Now;
                            await _context.SaveChangesAsync();

                            return await SignInUserAsync(utilisateur, model.SeSouvenirDeMoi, returnUrl);
                        }
                        else
                        {
                            // Incrémenter le compteur d'échecs de connexion
                            utilisateur.NbrEchecsAcces++;
                            _logger.LogWarning("Failed login attempt for {Email}. Number of failures: {Count}", model.Email, utilisateur.NbrEchecsAcces);

                            // Si le nombre d'échecs dépasse le seuil, verrouiller le compte
                            if (utilisateur.NbrEchecsAcces >= MaxFailedAccessAttempts)
                            {
                                utilisateur.FinLockout = DateTime.UtcNow.Add(LockoutTimeSpan);
                                _logger.LogWarning("Account locked for {Email} until {LockoutEnd}", model.Email, utilisateur.FinLockout.Value);
                                ModelState.AddModelError(string.Empty, $"Your account has been locked because of too many unsuccessful attempts. Please try again after {utilisateur.FinLockout.Value.ToLocalTime()}.");
                            }

                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact the platform manager.");
                        _logger.LogWarning("Login attempt on an inactive account: {Email}", model.Email);
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Tentative de connexion invalide.");
                    _logger.LogWarning("Login attempt for a non-existent email: {Email}", model.Email);
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
                    ModelState.AddModelError("MotDePasse", "Password must contain at least 8 characters, including an uppercase letter, a lowercase letter, a digit, and a special character.");
                    _logger.LogWarning("Weak password provided for account request: {Email}", model.Email);
                }

                // Retrieve the selected enterprise with its email domains.
                var entreprise = await _context.Entreprise
                    .Include(e => e.DomaineEmails)
                    .FirstOrDefaultAsync(e => e.Id == model.IdEntreprise);

                if (entreprise == null)
                {
                    ModelState.AddModelError("", "Organization is required.");
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
                        ModelState.AddModelError("Email", "The email domain does not match the selected organization.");
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
                    var verificationToken = SecurityTokenHelper.GenerateSecureToken();
                    var verificationTokenHash = SecurityTokenHelper.ComputeSha256(verificationToken);
                    var demande = new DemandeDeCompte
                    {
                        Nom = model.Nom,
                        Prenom = model.Prenom,
                        Email = model.Email,
                        MotDePasseHash = hashedPassword,
                        IdEntreprise = model.IdEntreprise,
                        IdStatutDeLaDemande = statutEnAttenteId,
                        EmailVerifie = false,
                        Commentaire = model.Commentaire != null ? model.Commentaire : "_",
                        DateCreation = DateTime.UtcNow,
                        VerificationToken = verificationTokenHash,
                        VerificationTokenExpiration = DateTime.UtcNow.AddHours(48)
                    };
                    _context.DemandeDeCompte.Add(demande);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Account request created for {Email}", model.Email);

                    var verificationLink = BuildAbsoluteUrl(Url.Action("VerifierEmailDemande", "Compte", new { token = verificationToken }, Request.Scheme));
                    await _accountEmailService.SendAccountRequestVerificationAsync(demande, verificationLink);
                }
                else
                {
                    // Refresh verification token if still pending and not verified
                    if (!existingRequest.EmailVerifie)
                    {
                        var verificationToken = SecurityTokenHelper.GenerateSecureToken();
                        existingRequest.VerificationToken = SecurityTokenHelper.ComputeSha256(verificationToken);
                        existingRequest.VerificationTokenExpiration = DateTime.UtcNow.AddHours(48);
                        await _context.SaveChangesAsync();

                        var verificationLink = BuildAbsoluteUrl(Url.Action("VerifierEmailDemande", "Compte", new { token = verificationToken }, Request.Scheme));
                        await _accountEmailService.SendAccountRequestVerificationAsync(existingRequest, verificationLink);
                        _logger.LogInformation("Resent verification email for existing request {Email}", model.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Account request already exists for {Email}", model.Email);
                    }
                }

                TempData["Success"] = "Your account request has been received. Please check your email to verify your address.";
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

                TempData["SuccessMessage"] = "Your profile has been updated successfully!";
                return RedirectToAction("Profil");
            }

            _logger.LogWarning("Profile update failed due to invalid model state for user {Email}.", User.Identity.Name);
            return View("Profil", model);
        }

        // GET: /Compte/NotebookTokens
        [HttpGet]
        public async Task<IActionResult> NotebookTokens()
        {
            var utilisateur = await GetCurrentUserAsync();
            if (utilisateur == null)
            {
                _logger.LogWarning("Notebook tokens page requested without a logged in user.");
                return RedirectToAction("SeConnecter", "Compte");
            }

            var model = await BuildNotebookTokensViewModel(utilisateur);
            model.NewToken = TempData["NotebookToken"] as string;

            return View(model);
        }

        // POST: /Compte/NotebookTokens/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNotebookToken(NotebookTokensViewModel model)
        {
            var utilisateur = await GetCurrentUserAsync();
            if (utilisateur == null)
            {
                _logger.LogWarning("Notebook token creation attempted without a logged in user.");
                return RedirectToAction("SeConnecter", "Compte");
            }

            if (!ModelState.IsValid)
            {
                var viewModel = await BuildNotebookTokensViewModel(utilisateur);
                viewModel.Label = model.Label;
                return View("NotebookTokens", viewModel);
            }

            var token = SecurityTokenHelper.GenerateSecureToken();
            var tokenHash = SecurityTokenHelper.ComputeSha256(token);

            var notebookToken = new NotebookApiToken
            {
                Label = model.Label?.Trim() ?? string.Empty,
                TokenHash = tokenHash,
                CreatedAtUtc = DateTime.UtcNow,
                IdUtilisateur = utilisateur.Id
            };

            _context.NotebookApiTokens.Add(notebookToken);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Notebook token created. Copy it now as it will not be shown again.";
            TempData["NotebookToken"] = token;

            return RedirectToAction("NotebookTokens");
        }

        // POST: /Compte/NotebookTokens/Revoke
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeNotebookToken(int id)
        {
            var utilisateur = await GetCurrentUserAsync();
            if (utilisateur == null)
            {
                _logger.LogWarning("Notebook token revoke attempted without a logged in user.");
                return RedirectToAction("SeConnecter", "Compte");
            }

            var notebookToken = await _context.NotebookApiTokens
                .FirstOrDefaultAsync(t => t.Id == id && t.IdUtilisateur == utilisateur.Id);

            if (notebookToken == null)
            {
                return NotFound();
            }

            if (!notebookToken.RevokedAtUtc.HasValue)
            {
                notebookToken.RevokedAtUtc = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Notebook token revoked.";
            return RedirectToAction("NotebookTokens");
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

        private async Task<Utilisateur?> GetCurrentUserAsync()
        {
            var userEmail = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                return null;
            }

            return await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == userEmail);
        }

        private async Task<NotebookTokensViewModel> BuildNotebookTokensViewModel(Utilisateur utilisateur)
        {
            var tokens = await _context.NotebookApiTokens
                .AsNoTracking()
                .Where(t => t.IdUtilisateur == utilisateur.Id)
                .OrderByDescending(t => t.CreatedAtUtc)
                .Select(t => new NotebookTokenListItemViewModel
                {
                    Id = t.Id,
                    Label = t.Label,
                    CreatedAtUtc = t.CreatedAtUtc,
                    LastUsedAtUtc = t.LastUsedAtUtc,
                    RevokedAtUtc = t.RevokedAtUtc
                })
                .ToListAsync();

            return new NotebookTokensViewModel
            {
                Tokens = tokens
            };
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
                    ModelState.AddModelError("MotDePasseActuel", "The current password is incorrect.");
                    _logger.LogWarning("Incorrect current password provided by user {Email} during change password.", userEmail);
                    return View(model);
                }

                if (!IsPasswordSecure(model.NouveauMotDePasse))
                {
                    ModelState.AddModelError("NouveauMotDePasse", "The new password is not secure enough. It must contain at least 8 characters, an uppercase letter, a lowercase letter, a digit, and a special character.");
                    _logger.LogWarning("Insecure new password provided by user {Email}.", userEmail);
                    return View(model);
                }
                utilisateur.MotDePasseHash = _passwordHasher.HashPassword(utilisateur, model.NouveauMotDePasse);
                utilisateur.DateModification = DateTime.Now;
                _context.Update(utilisateur);
                await _context.SaveChangesAsync();
                await _accountEmailService.SendPasswordChangedAsync(utilisateur);

                //TODO: Send email to user for password change Notification
                _logger.LogInformation("Password updated successfully for {Email}.", userEmail);
                TempData["SuccessMessage"] = "Your password has been updated successfully!";
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

        [HttpGet]
        public IActionResult VerifierMfa()
        {
            var email = HttpContext.Session.GetString("PendingMfaEmail");
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("SeConnecter");
            }

            bool.TryParse(HttpContext.Session.GetString("PendingMfaRememberMe"), out var rememberMe);
            var returnUrl = HttpContext.Session.GetString("PendingMfaReturnUrl");

            var vm = new VerifierMfaViewModel
            {
                Email = email,
                SeSouvenirDeMoi = rememberMe,
                ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? null : returnUrl
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifierMfa(VerifierMfaViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var sessionEmail = HttpContext.Session.GetString("PendingMfaEmail");
            if (!string.Equals(sessionEmail, model.Email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "The verification session has expired. Please sign in again.";
                ClearMfaSession();
                return RedirectToAction("SeConnecter");
            }

            if (await IsRateLimitedAsync($"mfa-verify:{model.Email.ToLowerInvariant()}", 5, TimeSpan.FromMinutes(10)))
            {
                TempData["Error"] = "Too many verification attempts. Please sign in again to request a new code.";
                ClearMfaSession();
                return RedirectToAction("SeConnecter");
            }

            var utilisateur = await _context.Utilisateur.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == model.Email);
            if (utilisateur == null || string.IsNullOrWhiteSpace(utilisateur.MfaCodeHash) || !utilisateur.MfaCodeExpiration.HasValue)
            {
                TempData["Error"] = "Verification code invalid or expired. Please sign in again.";
                ClearMfaSession();
                return RedirectToAction("SeConnecter");
            }

            if (utilisateur.MfaCodeExpiration.Value < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "The verification code has expired. Please request a new one by signing in again.");
                return View(model);
            }

            var providedHash = SecurityTokenHelper.ComputeSha256(model.Code);
            if (!string.Equals(providedHash, utilisateur.MfaCodeHash, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "The verification code is incorrect.");
                return View(model);
            }

            utilisateur.MfaCodeHash = null;
            utilisateur.MfaCodeExpiration = null;
            utilisateur.DernierLogin = DateTime.Now;
            await _context.SaveChangesAsync();
            ClearMfaSession();

            return await SignInUserAsync(utilisateur, model.SeSouvenirDeMoi, model.ReturnUrl);
        }

        // POST: /Compte/MotDePasseOublie
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MotDePasseOublie(MotDePasseOublieViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (await IsRateLimitedAsync($"pwd-reset-req:{model.Email.ToLowerInvariant()}", 3, TimeSpan.FromMinutes(60)))
            {
                TempData["Error"] = "Too many reset requests. Please try again in a little while.";
                return RedirectToAction("MotDePasseOublie");
            }

            var utilisateur = await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == model.Email && u.CompteActif);
            if (utilisateur != null)
            {
                var token = SecurityTokenHelper.GenerateSecureToken();
                utilisateur.PasswordResetTokenHash = SecurityTokenHelper.ComputeSha256(token);
                utilisateur.PasswordResetTokenExpiration = DateTime.UtcNow.AddHours(1);
                await _context.SaveChangesAsync();

                var resetLink = BuildAbsoluteUrl(Url.Action("ReinitialiserMotDePasse", "Compte", new { email = utilisateur.Email, token }, Request.Scheme));
                await _accountEmailService.SendForgotPasswordAsync(utilisateur, resetLink);
            }

            TempData["Success"] = "If an account exists for this email, you will receive a reset link shortly.";
            return RedirectToAction("MotDePasseOublie");
        }

        [HttpGet]
        public IActionResult ReinitialiserMotDePasse(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid password reset link.";
                return RedirectToAction("SeConnecter");
            }

            return View(new ResetMotDePasseViewModel
            {
                Email = email,
                Token = token
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReinitialiserMotDePasse(ResetMotDePasseViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var utilisateur = await _context.Utilisateur.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (utilisateur == null || string.IsNullOrWhiteSpace(utilisateur.PasswordResetTokenHash) || !utilisateur.PasswordResetTokenExpiration.HasValue)
            {
                TempData["Error"] = "Invalid password reset token.";
                return RedirectToAction("SeConnecter");
            }

            if (utilisateur.PasswordResetTokenExpiration.Value < DateTime.UtcNow)
            {
                TempData["Error"] = "This password reset link has expired.";
                return RedirectToAction("MotDePasseOublie");
            }

            var providedHash = SecurityTokenHelper.ComputeSha256(model.Token);
            if (!string.Equals(providedHash, utilisateur.PasswordResetTokenHash, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Invalid password reset token.";
                return RedirectToAction("SeConnecter");
            }

            if (!IsPasswordSecure(model.NouveauMotDePasse))
            {
                ModelState.AddModelError(nameof(model.NouveauMotDePasse), "Password must contain at least 8 characters, including an uppercase letter, a lowercase letter, a digit, and a special character.");
                return View(model);
            }

            utilisateur.MotDePasseHash = _passwordHasher.HashPassword(utilisateur, model.NouveauMotDePasse);
            utilisateur.PasswordResetTokenHash = null;
            utilisateur.PasswordResetTokenExpiration = null;
            utilisateur.DateModification = DateTime.Now;
            await _context.SaveChangesAsync();
            await _accountEmailService.SendPasswordChangedAsync(utilisateur);

            TempData["Success"] = "Your password has been reset. You can now sign in.";
            return RedirectToAction("SeConnecter");
        }

        [HttpGet]
        public async Task<IActionResult> VerifierEmailDemande(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid verification link.";
                return RedirectToAction("SeConnecter");
            }

            var tokenHash = SecurityTokenHelper.ComputeSha256(token);
            var demande = await _context.DemandeDeCompte.FirstOrDefaultAsync(d =>
                d.VerificationToken == tokenHash || d.VerificationToken == token);
            if (demande == null || (demande.VerificationTokenExpiration.HasValue && demande.VerificationTokenExpiration.Value < DateTime.UtcNow))
            {
                TempData["Error"] = "This verification link is invalid or has expired.";
                return RedirectToAction("SeConnecter");
            }

            demande.EmailVerifie = true;
            demande.EmailVerifieLe = DateTime.UtcNow;
            demande.VerificationToken = null;
            demande.VerificationTokenExpiration = null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Your email has been verified. An administrator will review your request shortly.";
            return RedirectToAction("SeConnecter");
        }

        private async Task<IActionResult> SignInUserAsync(Utilisateur utilisateur, bool rememberMe, string? returnUrl)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, utilisateur.Email),
                new Claim("NomComplet", $"{utilisateur.Prenom} {utilisateur.Nom}"),
                new Claim("UserId", utilisateur.Id.ToString()),
                new Claim(ClaimTypes.Role, utilisateur.Role != null ? utilisateur.Role.Libelle : "Viewer")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
            };

            if (rememberMe)
            {
                authProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                authProperties);
            _logger.LogInformation("Utilisateur {Email} logged in successfully.", utilisateur.Email);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Accueil");
        }

        private void ClearMfaSession()
        {
            HttpContext.Session.Remove("PendingMfaEmail");
            HttpContext.Session.Remove("PendingMfaReturnUrl");
            HttpContext.Session.Remove("PendingMfaRememberMe");
        }

        private string BuildAbsoluteUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.ToString();
            }

            var baseUrl = !string.IsNullOrWhiteSpace(_portalOptions.PublicBaseUrl)
                ? _portalOptions.PublicBaseUrl.TrimEnd('/')
                : $"{Request.Scheme}://{Request.Host}";

            if (!url.StartsWith("/"))
            {
                url = "/" + url;
            }

            return $"{baseUrl}{url}";
        }

        private async Task<bool> IsRateLimitedAsync(string key, int limit, TimeSpan window)
        {
            var normalizedKey = key.ToLowerInvariant();
            var now = DateTime.UtcNow;
            var windowEnd = now.Add(window);

            var entry = await _context.RateLimitEntries.FirstOrDefaultAsync(r => r.Key == normalizedKey);
            if (entry == null || entry.WindowEndUtc < now)
            {
                if (entry == null)
                {
                    entry = new RateLimitEntry
                    {
                        Key = normalizedKey,
                        Count = 1,
                        WindowEndUtc = windowEnd
                    };
                    _context.RateLimitEntries.Add(entry);
                }
                else
                {
                    entry.Count = 1;
                    entry.WindowEndUtc = windowEnd;
                    _context.RateLimitEntries.Update(entry);
                }
                await _context.SaveChangesAsync();
                return false;
            }

            entry.Count++;
            entry.WindowEndUtc = windowEnd;
            _context.RateLimitEntries.Update(entry);
            await _context.SaveChangesAsync();
            return entry.Count > limit;
        }
    }
}