using Dataportal.Context;
using Dataportal.Classes;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

//TODO: only allow rquestes from users who have verified there emails to be accepted
//TODO: send emails to user accepting or rejecting there request
//TODO: send email asking users to rechange ther password if there requeast is accepted

namespace Dataportal.Controllers
{
    public class GestionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GestionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Gestion/DemandeDeCompte
        [HttpGet]
        [Authorize(Roles = "administrator")]
        public IActionResult DemandeDeCompte(string search, int? selectedEntrepriseId, int? selectedStatutId, DateTime? dateMin, DateTime? dateMax)
        {
            var query = _context.DemandeDeCompte
                .Include(d => d.Entreprise)
                .Include(d => d.StatutDeLaDemande)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Nom.Contains(search) ||
                    d.Prenom.Contains(search) ||
                    d.Email.Contains(search) ||
                    d.Commentaire.Contains(search));
            }

            if (selectedEntrepriseId.HasValue)
                query = query.Where(d => d.IdEntreprise == selectedEntrepriseId);

            if (selectedStatutId.HasValue)
                query = query.Where(d => d.IdStatutDeLaDemande == selectedStatutId);

            if (dateMin.HasValue)
                query = query.Where(d => d.DateCreation >= dateMin.Value);

            if (dateMax.HasValue)
                query = query.Where(d => d.DateCreation <= dateMax.Value);

            var viewModel = new DemandeDeCompteViewModel
            {
                Demandes = query.ToList(),
                Entreprises = _context.Entreprise.Where(e => e.Actif).ToList(),
                Statuts = _context.StatutDeLaDemande.ToList(),
                Search = search,
                SelectedEntrepriseId = selectedEntrepriseId,
                SelectedStatutId = selectedStatutId,
                DateMin = dateMin,
                DateMax = dateMax,
                Roles = _context.Role.ToList()
            };

            return View(viewModel);
        }

        // GET: /Gestion/DemandeDeCompteDetails/5 (for AJAX modal)
        [HttpGet]
        [Authorize(Roles = "administrator")]
        public IActionResult DemandeDeCompteDetails(int id)
        {
            var demande = _context.DemandeDeCompte
                .Include(d => d.Entreprise)
                .Include(d => d.StatutDeLaDemande)
                .FirstOrDefault(d => d.Id == id);

            if (demande == null)
                return NotFound();

            return Json(new
            {
                demande.Id,
                demande.Nom,
                demande.Prenom,
                demande.Email,
                demande.Commentaire,
                demande.IdEntreprise,
                demande.IdStatutDeLaDemande,
                demande.EmailVerifie,
                DateCreation = demande.DateCreation.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        // POST: /Gestion/ApprouverDemande (submit approve from modal)
        [HttpPost]
        [Authorize(Roles = "administrator")]
        public IActionResult ApprouverDemande(int id, int idStatut, int idEntreprise, bool emailVerifie, string commentaire, int? idRole)
        {
            var demande = _context.DemandeDeCompte.FirstOrDefault(d => d.Id == id);
            if (demande == null)
                return NotFound();

            var pendingOrRejectedStatuses = _context.StatutDeLaDemande
                .Where(s => s.Libelle != null)
                .Select(s => new { s.Id, Libelle = s.Libelle.ToLower() })
                .Where(s => s.Libelle == "pending" || s.Libelle == "rejected")
                .Select(s => s.Id)
                .ToList();

            if (!pendingOrRejectedStatuses.Contains(demande.IdStatutDeLaDemande))
                return Forbid();

            if (idStatut == StatutDeLaDemandeIds.Valider) // Approving
            {
                // Ensure role was selected
                if (!idRole.HasValue)
                {
                    TempData["Error"] = "You must select a role to approve the request.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Check if role exists
                var role = _context.Role.FirstOrDefault(r => r.Id == idRole.Value);
                if (role == null)
                {
                    TempData["Error"] = "Invalid role.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Check email doesn't exist
                if (_context.Utilisateur.Any(u => u.Email == demande.Email))
                {
                    TempData["Error"] = "A user with this email already exists.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Create user
                var nouvelUtilisateur = new Utilisateur
                {
                    Nom = demande.Nom,
                    Prenom = demande.Prenom,
                    Email = demande.Email,
                    MotDePasseHash = demande.MotDePasseHash,
                    IdEntreprise = idEntreprise,
                    IdRole = idRole.Value,
                    CompteActif = true,
                    DateApprobation = DateTime.Now,
                    LienLinkedIn = string.Empty,
                    DescriptionProfil = string.Empty
                };

                _context.Utilisateur.Add(nouvelUtilisateur);
            }

            // Always update demande
            demande.IdStatutDeLaDemande = idStatut;
            demande.IdEntreprise = idEntreprise;
            demande.EmailVerifie = emailVerifie;
            demande.Commentaire = commentaire;

            _context.SaveChanges();

            return RedirectToAction("DemandeDeCompte");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult DeleteDemande(int id)
        {
            var demande = _context.DemandeDeCompte.FirstOrDefault(d => d.Id == id);
            if (demande == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction("DemandeDeCompte");
            }

            if (demande.IdStatutDeLaDemande != StatutDeLaDemandeIds.Refuser)
            {
                TempData["Error"] = "Only rejected requests can be deleted.";
                return RedirectToAction("DemandeDeCompte");
            }

            _context.DemandeDeCompte.Remove(demande);
            _context.SaveChanges();

            TempData["Success"] = "Request deleted successfully.";
            return RedirectToAction("DemandeDeCompte");
        }

        // GET: /Gestion/Entreprises
        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult Entreprises(string search, bool? actif)
        {
            var query = _context.Entreprise.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.Nom.Contains(search));

            if (actif.HasValue)
                query = query.Where(e => e.Actif == actif.Value);

            var vm = new EntrepriseViewModel
            {
                Entreprises = query.ToList(),
                Search = search,
                Actif = actif
            };

            return View(vm);
        }

        //Post: /Gestion/CreateEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult CreateEntreprise(string Nom, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Organization name is required.";
                return RedirectToAction("Entreprises");
            }

            var normalizedNom = Nom.Trim().ToLower();

            var exists = _context.Entreprise
                .Any(e => e.Nom.Trim().ToLower() == normalizedNom);

            if (exists)
            {
                TempData["Error"] = "An organization with this name already exists.";
                return RedirectToAction("Entreprises");
            }

            var entreprise = new Entreprise
            {
                Nom = Nom,
                Actif = Actif
            };

            _context.Entreprise.Add(entreprise);
            _context.SaveChanges();

            TempData["Success"] = "Organization created successfully.";
            return RedirectToAction("Entreprises");
        }

        // POST: /Gestion/ActivateEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult ActivateEntreprise(int id)
        {
            var entreprise = _context.Entreprise.FirstOrDefault(e => e.Id == id);
            if (entreprise == null)
            {
                TempData["Error"] = "Organization not found.";
                return RedirectToAction("Entreprises");
            }

            entreprise.Actif = true;
            _context.SaveChanges();

            TempData["Success"] = "Organization activated successfully.";
            return RedirectToAction("Entreprises");
        }

        // POST: /Gestion/DeactivateEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult DeactivateEntreprise(int id)
        {
            var entreprise = _context.Entreprise.FirstOrDefault(e => e.Id == id);
            if (entreprise == null)
            {
                TempData["Error"] = "Organization not found.";
                return RedirectToAction("Entreprises");
            }

            entreprise.Actif = false;
            _context.SaveChanges();

            TempData["Success"] = "Organization deactivated successfully.";
            return RedirectToAction("Entreprises");
        }

        // POST: /Gestion/DeleteEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult DeleteEntreprise(int id)
        {
            var entreprise = _context.Entreprise
                .Include(e => e.Utilisateurs)
                .Include(e => e.DemandeDeComptes)
                .FirstOrDefault(e => e.Id == id);

            if (entreprise == null)
            {
                TempData["Error"] = "Organization not found.";
                return RedirectToAction("Entreprises");
            }

            if (entreprise.Utilisateurs.Any() || entreprise.DemandeDeComptes.Any())
            {
                TempData["Error"] = "Unable to delete this organization because it has users or account requests.";
                return RedirectToAction("Entreprises");
            }

            _context.Entreprise.Remove(entreprise);
            _context.SaveChanges();

            TempData["Success"] = "Organization deleted successfully.";
            return RedirectToAction("Entreprises");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult EditEntreprise(int id, string Nom, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Organization name is required.";
                return RedirectToAction("Entreprises");
            }

            var entreprise = _context.Entreprise.FirstOrDefault(e => e.Id == id);
            if (entreprise == null)
            {
                TempData["Error"] = "Organization not found.";
                return RedirectToAction("Entreprises");
            }

            var normalizedNom = Nom.Trim().ToLower();
            var exists = _context.Entreprise.Any(e => e.Id != id && e.Nom.Trim().ToLower() == normalizedNom);

            if (exists)
            {
                TempData["Error"] = "An organization with this name already exists.";
                return RedirectToAction("Entreprises");
            }

            entreprise.Nom = Nom.Trim();
            entreprise.Actif = Actif;
            _context.SaveChanges();

            TempData["Success"] = "Organization updated successfully.";
            return RedirectToAction("Entreprises");
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult GetDomaines(int id)
        {
            var entreprise = _context.Entreprise
                .Include(e => e.DomaineEmails)
                .FirstOrDefault(e => e.Id == id);

            if (entreprise == null)
            {
                var errorVm = new ModalErrorViewModel
                {
                    Title = "Organization unavailable",
                    Message = "The selected organization could not be found. It may have been removed or you no longer have access to it."
                };

                return PartialView("_ModalError", errorVm);
            }

            var vm = new EntrepriseDomainesViewModel
            {
                EntrepriseId = entreprise.Id,
                EntrepriseNom = entreprise.Nom,
                Domaines = entreprise.DomaineEmails
                    .Select(d => new DomaineEmailViewModel
                    {
                        Id = d.Id,
                        Domaine = d.Domaine,
                        DomaineActif = d.DomaineActif
                    }).ToList()
            };

            return PartialView("_ManageDomainesModal", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult AddDomaine(int entrepriseId, string newDomaine, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(newDomaine))
            {
                TempData["Error"] = "Domain is required.";
                return RedirectToAction("Entreprises");
            }

            var cleaned = newDomaine.Trim().ToLower();

            // Validate structure (very simple check)
            if (!cleaned.Contains(".") || cleaned.StartsWith(".") || cleaned.EndsWith("."))
            {
                TempData["Error"] = "Invalid domain format.";
                return RedirectToAction("Entreprises");
            }

            var exists = _context.DomaineEmail
                .Any(d => d.Domaine.ToLower() == cleaned && d.IdEntreprise == entrepriseId);

            if (exists)
            {
                TempData["Error"] = "This domain already exists for this organization.";
                return RedirectToAction("Entreprises");
            }

            var domaine = new DomaineEmail
            {
                IdEntreprise = entrepriseId,
                Domaine = cleaned,
                DomaineActif = Actif
            };

            _context.DomaineEmail.Add(domaine);
            _context.SaveChanges();

            TempData["Success"] = "Domain added successfully.";
            return RedirectToAction("Entreprises");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult ToggleDomaineActif(int id)
        {
            var domaine = _context.DomaineEmail.FirstOrDefault(d => d.Id == id);
            if (domaine == null)
            {
                TempData["Error"] = "Domain not found.";
                return RedirectToAction("Entreprises");
            }

            domaine.DomaineActif = !domaine.DomaineActif;
            _context.SaveChanges();

            TempData["Success"] = "Domain status updated.";
            return RedirectToAction("Entreprises");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult DeleteDomaine(int id)
        {
            var domaine = _context.DomaineEmail
                .Include(d => d.Entreprise)
                .FirstOrDefault(d => d.Id == id);

            if (domaine == null)
            {
                TempData["Error"] = "Domain not found.";
                return RedirectToAction("Entreprises");
            }

            // Check for linked users
            var hasUsers = _context.Utilisateur.Any(u => u.IdEntreprise == domaine.IdEntreprise && u.Email.EndsWith("@" + domaine.Domaine));
            var hasRequests = _context.DemandeDeCompte.Any(r => r.IdEntreprise == domaine.IdEntreprise && r.Email.EndsWith("@" + domaine.Domaine));

            if (hasUsers || hasRequests)
            {
                TempData["Error"] = "Unable to delete this domain because it is used by users or account requests.";
                return RedirectToAction("Entreprises");
            }

            _context.DomaineEmail.Remove(domaine);
            _context.SaveChanges();

            TempData["Success"] = "Domain deleted successfully.";
            return RedirectToAction("Entreprises");
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult Utilisateurs(string search, int? selectedEntrepriseId, int? selectedRoleId, bool? compteActif)
        {
            var query = _context.Utilisateur
                .Include(u => u.Entreprise)
                .Include(u => u.Role)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(u =>
                    u.Nom.Contains(search) ||
                    u.Prenom.Contains(search) ||
                    u.Email.Contains(search));
            }

            if (selectedEntrepriseId.HasValue)
            {
                query = query.Where(u => u.IdEntreprise == selectedEntrepriseId.Value);
            }

            if (selectedRoleId.HasValue)
            {
                query = query.Where(u => u.IdRole == selectedRoleId.Value);
            }

            if (compteActif.HasValue)
            {
                query = query.Where(u => u.CompteActif == compteActif.Value);
            }

            var vm = new UtilisateursAdminViewModel
            {
                Utilisateurs = query.ToList(),
                Entreprises = _context.Entreprise.Where(e => e.Actif).ToList(),
                Roles = _context.Role.ToList(),
                Search = search,
                SelectedEntrepriseId = selectedEntrepriseId,
                SelectedRoleId = selectedRoleId,
                CompteActif = compteActif
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult ToggleUtilisateurActif(int id)
        {
            //TODO: send email on activation or deactivation of the user
            var utilisateur = _context.Utilisateur
                .Include(u => u.Role)
                .FirstOrDefault(u => u.Id == id);

            if (utilisateur == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Utilisateurs");
            }

            // Prevent administrators from deactivating another administrator
            if (utilisateur.IdRole == RoleIds.Administrateur && utilisateur.CompteActif)
            {
                TempData["Error"] = "Cannot deactivate an administrator account.";
                return RedirectToAction("Utilisateurs");
            }

            utilisateur.CompteActif = !utilisateur.CompteActif;

            if (utilisateur.CompteActif)
            {
                // Reactivation removes any lockout that might still be active
                utilisateur.FinLockout = null;
            }

            utilisateur.DateModification = DateTime.Now;
            _context.SaveChanges();

            TempData["Success"] = "Account status updated.";
            return RedirectToAction("Utilisateurs");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public IActionResult EditUserRole(int id, int newRoleId)
        {
            var utilisateur = _context.Utilisateur.FirstOrDefault(u => u.Id == id);
            if (utilisateur == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Utilisateurs");
            }

            var role = _context.Role.FirstOrDefault(r => r.Id == newRoleId);
            if (role == null)
            {
                TempData["Error"] = "Invalid role.";
                return RedirectToAction("Utilisateurs");
            }

            utilisateur.IdRole = newRoleId;
            _context.SaveChanges();

            TempData["Success"] = "User role updated.";
            return RedirectToAction("Utilisateurs");
        }
    }
}
