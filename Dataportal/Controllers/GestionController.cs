using Dataportal.Context;
using Dataportal.Classes;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dataportal.Services.Email;
using Dataportal.Services;

namespace Dataportal.Controllers
{
    public class GestionController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccountEmailService _accountEmailService;
        private readonly IndexMaintenanceService _indexMaintenanceService;
        private readonly NotebookReplaceSessionService _replaceSessionService;

        public GestionController(ApplicationDbContext context, IAccountEmailService accountEmailService, IndexMaintenanceService indexMaintenanceService, NotebookReplaceSessionService replaceSessionService)
        {
            _context = context;
            _accountEmailService = accountEmailService;
            _indexMaintenanceService = indexMaintenanceService;
            _replaceSessionService = replaceSessionService;
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public async Task<IActionResult> Indexation(string? search, string? status, string? type)
        {
            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
            var isAdmin = User.IsInRole("administrator");

            var donneesQuery = _context.Donnees
                .AsNoTracking()
                .Include(d => d.Metadonnee)
                .Where(d => d.IndexEnabled)
                .AsQueryable();

            var eventLogsQuery = _context.DonneesEventLogs
                .AsNoTracking()
                .Include(d => d.Metadonnee)
                .Where(d => d.IndexEnabled)
                .AsQueryable();

            var contexteQuery = _context.DonneesContexteEnvironnemental
                .AsNoTracking()
                .Include(d => d.Metadonnee)
                .Where(d => d.IndexEnabled)
                .AsQueryable();

            if (!isAdmin && userId.HasValue)
            {
                donneesQuery = donneesQuery.Where(d => d.Metadonnee != null && d.Metadonnee.IdUtilisateur == userId.Value);
                eventLogsQuery = eventLogsQuery.Where(d => d.Metadonnee != null && d.Metadonnee.IdUtilisateur == userId.Value);
                contexteQuery = contexteQuery.Where(d => d.Metadonnee != null && d.Metadonnee.IdUtilisateur == userId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                donneesQuery = donneesQuery.Where(d => d.IndexStatus == status);
                eventLogsQuery = eventLogsQuery.Where(d => d.IndexStatus == status);
                contexteQuery = contexteQuery.Where(d => d.IndexStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                donneesQuery = donneesQuery.Where(d =>
                    d.Libelle.Contains(search) ||
                    d.NomDeLaTable.Contains(search));
                eventLogsQuery = eventLogsQuery.Where(d =>
                    d.Libelle.Contains(search) ||
                    d.NomDeLaTable.Contains(search));
                contexteQuery = contexteQuery.Where(d =>
                    d.Libelle.Contains(search) ||
                    d.NomDeLaTable.Contains(search));
            }

            var jobs = new List<IndexationJobItemViewModel>();

            if (string.IsNullOrWhiteSpace(type) || type == "donnees")
            {
                jobs.AddRange(await donneesQuery
                    .Select(d => new IndexationJobItemViewModel
                    {
                        RecordId = d.Id,
                        RecordType = "donnees",
                        MetadonneeId = d.IdMetadonnee,
                        DatasetName = d.Metadonnee != null ? d.Metadonnee.Nom : d.Libelle,
                        TableName = d.NomDeLaTable,
                        IndexName = d.IndexName ?? string.Empty,
                        IndexStatus = d.IndexStatus ?? "not enabled",
                        IndexTimeColumn = d.IndexTimeColumn,
                        IndexIdColumn = d.IndexIdColumn,
                        IndexIncludeColumn = d.IndexIncludeColumn,
                        IndexError = d.IndexError
                    })
                    .ToListAsync());
            }

            if (string.IsNullOrWhiteSpace(type) || type == "eventlogs")
            {
                jobs.AddRange(await eventLogsQuery
                    .Select(d => new IndexationJobItemViewModel
                    {
                        RecordId = d.Id,
                        RecordType = "eventlogs",
                        MetadonneeId = d.IdMetadonnee,
                        DatasetName = d.Metadonnee != null ? d.Metadonnee.Nom : d.Libelle,
                        TableName = d.NomDeLaTable,
                        IndexName = d.IndexName ?? string.Empty,
                        IndexStatus = d.IndexStatus ?? "not enabled",
                        IndexTimeColumn = d.IndexTimeColumn,
                        IndexIdColumn = d.IndexIdColumn,
                        IndexIncludeColumn = d.IndexIncludeColumn,
                        IndexError = d.IndexError
                    })
                    .ToListAsync());
            }

            if (string.IsNullOrWhiteSpace(type) || type == "contexte")
            {
                jobs.AddRange(await contexteQuery
                    .Select(d => new IndexationJobItemViewModel
                    {
                        RecordId = d.Id,
                        RecordType = "contexte",
                        MetadonneeId = d.IdMetadonnee,
                        DatasetName = d.Metadonnee != null ? d.Metadonnee.Nom : d.Libelle,
                        TableName = d.NomDeLaTable,
                        IndexName = d.IndexName ?? string.Empty,
                        IndexStatus = d.IndexStatus ?? "not enabled",
                        IndexTimeColumn = d.IndexTimeColumn,
                        IndexIdColumn = d.IndexIdColumn,
                        IndexIncludeColumn = d.IndexIncludeColumn,
                        IndexError = d.IndexError
                    })
                    .ToListAsync());
            }

            var vm = new IndexationViewModel
            {
                Search = search,
                Status = status,
                Type = type,
                Jobs = jobs
                    .OrderByDescending(j => j.IndexStatus == "failed")
                    .ThenBy(j => j.DatasetName)
                    .ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunIndexationNow(string type, int id, string? returnUrl)
        {
            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
            var isAdmin = User.IsInRole("administrator");
            var target = IndexationTarget.From(type, id);
            if (string.IsNullOrWhiteSpace(target.Type))
            {
                TempData["Error"] = "Unknown indexation target.";
                return RedirectToAction("Indexation");
            }

            if (!isAdmin && userId.HasValue)
            {
                var ownsData = target.Type switch
                {
                    "donnees" => await _context.Donnees.AnyAsync(d => d.Id == id && d.Metadonnee.IdUtilisateur == userId.Value),
                    "eventlogs" => await _context.DonneesEventLogs.AnyAsync(d => d.Id == id && d.Metadonnee.IdUtilisateur == userId.Value),
                    "contexte" => await _context.DonneesContexteEnvironnemental.AnyAsync(d => d.Id == id && d.Metadonnee.IdUtilisateur == userId.Value),
                    _ => false
                };

                if (!ownsData)
                {
                    return Forbid();
                }
            }

            var result = await _indexMaintenanceService.RunIndexNowAsync(target, HttpContext.RequestAborted);
            TempData[result.Success ? "Success" : "Error"] = result.Message;

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Indexation");
        }

        // GET: /Gestion/NotebookApiAccessLogs
        [HttpGet]
        [Authorize(Roles = "administrator")]
        public async Task<IActionResult> NotebookApiAccessLogs(int? datasetId, int? userId, int? tokenId, DateTime? dateFromUtc, DateTime? dateToUtc)
        {
            var query = _context.NotebookApiAccessLogs
                .AsNoTracking()
                .Include(l => l.Metadonnee)
                .Include(l => l.Utilisateur)
                .Include(l => l.NotebookApiToken)
                .AsQueryable();

            if (datasetId.HasValue)
            {
                query = query.Where(l => l.IdMetadonnee == datasetId.Value);
            }

            if (userId.HasValue)
            {
                query = query.Where(l => l.IdUtilisateur == userId.Value);
            }

            if (tokenId.HasValue)
            {
                query = query.Where(l => l.IdNotebookApiToken == tokenId.Value);
            }

            if (dateFromUtc.HasValue)
            {
                query = query.Where(l => l.AccessedAtUtc >= dateFromUtc.Value);
            }

            if (dateToUtc.HasValue)
            {
                query = query.Where(l => l.AccessedAtUtc <= dateToUtc.Value);
            }

            var logs = await query
                .OrderByDescending(l => l.AccessedAtUtc)
                .Take(500)
                .Select(l => new NotebookApiAccessLogItemViewModel
                {
                    Id = l.Id,
                    DatasetId = l.IdMetadonnee,
                    DatasetName = l.Metadonnee != null ? l.Metadonnee.Nom : string.Empty,
                    UserId = l.IdUtilisateur,
                    UserDisplayName = l.Utilisateur != null ? $"{l.Utilisateur.Prenom} {l.Utilisateur.Nom}".Trim() : null,
                    TokenId = l.IdNotebookApiToken,
                    TokenLabel = l.NotebookApiToken != null ? l.NotebookApiToken.Label : null,
                    AccessedAtUtc = l.AccessedAtUtc,
                    BytesReturned = l.BytesReturned
                })
                .ToListAsync();

            var viewModel = new NotebookApiAccessLogsViewModel
            {
                DatasetId = datasetId,
                UserId = userId,
                TokenId = tokenId,
                DateFromUtc = dateFromUtc,
                DateToUtc = dateToUtc,
                Logs = logs
            };

            return View(viewModel);
        }

        // GET: /Gestion/NotebookReplaceSessions
        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public async Task<IActionResult> NotebookReplaceSessions(int? datasetId, int? userId, NotebookReplaceStatus? status, DateTime? createdFromUtc, DateTime? createdToUtc)
        {
            await _replaceSessionService.AbortExpiredSessionsAsync(DateTime.UtcNow.AddHours(-1), HttpContext.RequestAborted);

            var isAdmin = User.IsInRole("administrator");
            var currentUserId = HttpContextUserHelper.TryGetCurrentUserId(User);

            var query = _context.NotebookReplaceSessions
                .AsNoTracking()
                .Include(s => s.Metadonnee)
                .Include(s => s.Utilisateur)
                .AsQueryable();

            if (!isAdmin && currentUserId.HasValue)
            {
                query = query.Where(s => s.Metadonnee != null && s.Metadonnee.IdUtilisateur == currentUserId.Value);
            }

            if (datasetId.HasValue)
            {
                query = query.Where(s => s.IdMetadonnee == datasetId.Value);
            }

            if (userId.HasValue && isAdmin)
            {
                query = query.Where(s => s.IdUtilisateur == userId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            if (createdFromUtc.HasValue)
            {
                query = query.Where(s => s.CreatedAtUtc >= createdFromUtc.Value);
            }

            if (createdToUtc.HasValue)
            {
                query = query.Where(s => s.CreatedAtUtc <= createdToUtc.Value);
            }

            var sessions = await query
                .OrderByDescending(s => s.CreatedAtUtc)
                .Take(500)
                .Select(s => new NotebookReplaceSessionItemViewModel
                {
                    Id = s.Id,
                    DatasetId = s.IdMetadonnee,
                    DatasetName = s.Metadonnee != null ? s.Metadonnee.Nom : string.Empty,
                    Schema = s.Schema,
                    TableName = s.TableName,
                    StagingTableName = s.StagingTableName,
                    OldTableName = s.OldTableName,
                    Status = s.Status,
                    CreatedAtUtc = s.CreatedAtUtc,
                    UpdatedAtUtc = s.UpdatedAtUtc,
                    CompletedAtUtc = s.CompletedAtUtc,
                    UserId = s.IdUtilisateur,
                    UserDisplayName = s.Utilisateur != null ? $"{s.Utilisateur.Prenom} {s.Utilisateur.Nom}".Trim() : null
                })
                .ToListAsync();

            var statusOptions = Enum.GetValues(typeof(NotebookReplaceStatus))
                .Cast<NotebookReplaceStatus>()
                .Select(value => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = value.ToString(),
                    Text = value.ToString(),
                    Selected = status.HasValue && status.Value == value
                })
                .ToList();

            var viewModel = new NotebookReplaceSessionsViewModel
            {
                DatasetId = datasetId,
                UserId = userId,
                Status = status,
                CreatedFromUtc = createdFromUtc,
                CreatedToUtc = createdToUtc,
                StatusOptions = statusOptions,
                Sessions = sessions,
                IsAdminView = isAdmin
            };

            return View(viewModel);
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
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public async Task<IActionResult> ApprouverDemande(int id, int idStatut, int idEntreprise, bool emailVerifie, string commentaire, int? idRole)
        {
            var demande = await _context.DemandeDeCompte.FirstOrDefaultAsync(d => d.Id == id);
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
                if (!demande.EmailVerifie)
                {
                    TempData["Error"] = "The user's email must be verified before approval.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Ensure role was selected
                if (!idRole.HasValue)
                {
                    TempData["Error"] = "You must select a role to approve the request.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Check if role exists
                var role = await _context.Role.FirstOrDefaultAsync(r => r.Id == idRole.Value);
                if (role == null)
                {
                    TempData["Error"] = "Invalid role.";
                    return RedirectToAction("DemandeDeCompte");
                }

                // Check email doesn't exist
                if (await _context.Utilisateur.AnyAsync(u => u.Email == demande.Email))
                {
                    TempData["Error"] = "A user with this email already exists.";
                    return RedirectToAction("DemandeDeCompte");
                }

                var entreprise = await _context.Entreprise.FirstOrDefaultAsync(e => e.Id == idEntreprise);
                if (entreprise == null)
                {
                    TempData["Error"] = "Invalid organization.";
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
                    MfaEnabled = true,
                    MfaCodeHash = null,
                    MfaCodeExpiration = null,
                    LienLinkedIn = string.Empty,
                    DescriptionProfil = string.Empty
                };

                _context.Utilisateur.Add(nouvelUtilisateur);

                await _accountEmailService.SendAccountRequestApprovedAsync(demande.Email, role.Libelle, entreprise.Nom);
            }

            // Always update demande
            demande.IdStatutDeLaDemande = idStatut;
            demande.IdEntreprise = idEntreprise;
            demande.EmailVerifie = demande.EmailVerifie || emailVerifie;
            demande.Commentaire = commentaire;

            await _context.SaveChangesAsync();

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
        public async Task<IActionResult> ToggleUtilisateurActif(int id)
        {
            var utilisateur = await _context.Utilisateur
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == id);

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
            await _context.SaveChangesAsync();
            await _accountEmailService.SendActivationChangeAsync(utilisateur, utilisateur.CompteActif);

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

        [HttpGet]
        [Authorize(Roles = "administrator")]
        public async Task<IActionResult> MessageAccueil()
        {
            var message = await _context.MessageAccueil.FirstOrDefaultAsync();
            var viewModel = new MessageAccueilViewModel
            {
                Contenu = message?.Contenu ?? string.Empty,
                VisibleAuxInvites = message?.VisibleAuxInvites ?? false
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "administrator")]
        public async Task<IActionResult> MessageAccueil(MessageAccueilViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            var message = await _context.MessageAccueil.FirstOrDefaultAsync();

            if (message == null)
            {
                message = new MessageAccueil
                {
                    Contenu = viewModel.Contenu,
                    VisibleAuxInvites = viewModel.VisibleAuxInvites,
                    DateDerniereModification = DateTime.UtcNow
                };

                _context.MessageAccueil.Add(message);
            }
            else
            {
                message.Contenu = viewModel.Contenu;
                message.VisibleAuxInvites = viewModel.VisibleAuxInvites;
                message.DateDerniereModification = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Home message updated.";

            return RedirectToAction(nameof(MessageAccueil));
        }
    }
}
