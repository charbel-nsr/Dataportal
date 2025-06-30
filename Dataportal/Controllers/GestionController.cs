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
    [Authorize(Roles = "administrateur")]
    public class GestionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GestionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Gestion/DemandeDeCompte
        [HttpGet]
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
        public IActionResult ApprouverDemande(int id, int idStatut, int idEntreprise, bool emailVerifie, string commentaire, int? idRole)
        {
            var demande = _context.DemandeDeCompte.FirstOrDefault(d => d.Id == id);
            if (demande == null)
                return NotFound();

            var statutEnAttenteOuRefuse = _context.StatutDeLaDemande
                .Where(s => s.Libelle.ToLower().Contains("attente") || s.Libelle.ToLower().Contains("refus"))
                .Select(s => s.Id)
                .ToList();

            if (!statutEnAttenteOuRefuse.Contains(demande.IdStatutDeLaDemande))
                return Forbid();

            if (idStatut == 2) // Approving
            {
                // Ensure role was selected
                if (!idRole.HasValue)
                {
                    TempData["Error"] = "Vous devez sélectionner un rôle pour valider la demande.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Check if role exists
                var role = _context.Role.FirstOrDefault(r => r.Id == idRole.Value);
                if (role == null)
                {
                    TempData["Error"] = "Rôle invalide.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Check email doesn't exist
                if (_context.Utilisateur.Any(u => u.Email == demande.Email))
                {
                    TempData["Error"] = "Un utilisateur avec cet email existe déjà.";
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
                    DateApprobation = DateTime.Now
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

        // GET: /Gestion/Entreprises
        [HttpGet]
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
        public IActionResult CreateEntreprise(string Nom, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Le nom de l'entreprise est requis.";
                return RedirectToAction("Entreprises");
            }

            var normalizedNom = Nom.Trim().ToLower();

            var exists = _context.Entreprise
                .Any(e => e.Nom.Trim().ToLower() == normalizedNom);

            if (exists)
            {
                TempData["Error"] = "Une entreprise avec ce nom existe déjà.";
                return RedirectToAction("Entreprises");
            }

            var entreprise = new Entreprise
            {
                Nom = Nom,
                Actif = Actif
            };

            _context.Entreprise.Add(entreprise);
            _context.SaveChanges();

            TempData["Success"] = "Entreprise créée avec succès.";
            return RedirectToAction("Entreprises");
        }

        // POST: /Gestion/ActivateEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateEntreprise(int id)
        {
            var entreprise = _context.Entreprise.FirstOrDefault(e => e.Id == id);
            if (entreprise == null)
            {
                TempData["Error"] = "Entreprise introuvable.";
                return RedirectToAction("Entreprises");
            }

            entreprise.Actif = true;
            _context.SaveChanges();

            TempData["Success"] = "Entreprise activée avec succès.";
            return RedirectToAction("Entreprises");
        }

        // POST: /Gestion/DeactivateEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeactivateEntreprise(int id)
        {
            var entreprise = _context.Entreprise.FirstOrDefault(e => e.Id == id);
            if (entreprise == null)
            {
                TempData["Error"] = "Entreprise introuvable.";
                return RedirectToAction("Entreprises");
            }

            entreprise.Actif = false;
            _context.SaveChanges();

            TempData["Success"] = "Entreprise désactivée avec succès.";
            return RedirectToAction("Entreprises");
        }

        // POST: /Gestion/DeleteEntreprise
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteEntreprise(int id)
        {
            var entreprise = _context.Entreprise
                .Include(e => e.Utilisateurs)
                .Include(e => e.DemandeDeComptes)
                .FirstOrDefault(e => e.Id == id);

            if (entreprise == null)
            {
                TempData["Error"] = "Entreprise introuvable.";
                return RedirectToAction("Entreprises");
            }

            if (entreprise.Utilisateurs.Any() || entreprise.DemandeDeComptes.Any())
            {
                TempData["Error"] = "Impossible de supprimer cette entreprise car elle possède des utilisateurs ou des demandes de compte.";
                return RedirectToAction("Entreprises");
            }

            _context.Entreprise.Remove(entreprise);
            _context.SaveChanges();

            TempData["Success"] = "Entreprise supprimée avec succès.";
            return RedirectToAction("Entreprises");
        }
    }
}
