using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Helpers;
using Dataportal.Models;
using Dataportal.Services;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SystemTextJson = System.Text.Json.JsonSerializer;

namespace Dataportal.Controllers
{
    [Authorize(Roles = "administrator,editor,user")]
    public class ModifDonneesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITabularFileImporter _fileImporter;
        private const string UploadCacheFolderName = "dataportal-upload-cache";
        private const string UploadMetadataFileName = "metadata.json";

        private enum ExistingLabelSource
        {
            EventLogs,
            Contexte
        }

        public ModifDonneesController(ApplicationDbContext context, ITabularFileImporter fileImporter)
        {
            _context = context;
            _fileImporter = fileImporter;
        }

        [HttpGet]
        public IActionResult EditStep1(int id)
        {
            var metadonnee = _context.Metadonnee
                .Include(m => m.Metadonnee_Appareils)
                .FirstOrDefault(m => m.Id == id);

            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            var visibilites = _context.Visibilite.AsQueryable();
            if (User.IsInRole("user"))
            {
                visibilites = visibilites.Where(v => v.Id == VisibiliteIds.Personnelle);
            }

            var vm = new DonneesEditStep1ViewModel
            {
                Id = metadonnee.Id,
                Nom = metadonnee.Nom,
                Description = metadonnee.Description,
                IdLicence = metadonnee.IdLicence,
                IdSite = metadonnee.IdSite,
                IdVisibilite = metadonnee.IdVisibilite,
                IdTypeEnergieRenouvelable = metadonnee.IdTypeEnergieRenouvelable,
                SeriesTemporelles = metadonnee.SeriesTemporelles,
                AutoriserApi = metadonnee.AutoriserApi,
                Anonymiser = metadonnee.Anonymiser,
                AutoriserLeTelechargement = metadonnee.AutoriserLeTelechargement,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                Sites = _context.Site.Where(s => s.Actif).ToList(),
                Visibilites = visibilites.ToList(),
                TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList(),
                Appareils = _context.Appareil.Where(a => a.Actif).ToList(),
                SelectedAppareils = metadonnee.Metadonnee_Appareils?.Select(a => a.IdAppareil).ToList(),
                AppareilInfos = metadonnee.Metadonnee_Appareils?.Select(a => new MetadonneeAppareilInfo
                {
                    IdAppareil = a.IdAppareil,
                    IdAppareilDansDonnees = a.IdAppareilDansDonnees,
                    Commentaire = a.Commentaire
                }).ToList() ?? new List<MetadonneeAppareilInfo>()
            };

            HttpContext.Session.SetInt32(SessionKeys.ModificationMetadonneeId, metadonnee.Id);
            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 1);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditStep1(DonneesEditStep1ViewModel model)
        {
            var metadonnee = _context.Metadonnee
                .Include(m => m.Metadonnee_Appareils)
                .FirstOrDefault(m => m.Id == model.Id);

            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            if (User.IsInRole("user") && model.IdVisibilite != VisibiliteIds.Personnelle)
            {
                ModelState.AddModelError(nameof(model.IdVisibilite), "Standard users can only set personal visibility.");
            }

            if (!ModelState.IsValid)
            {
                PopulateStep1Selections(model);
                return View(model);
            }

            metadonnee.Description = model.Description?.Trim();
            metadonnee.IdLicence = model.IdLicence;
            metadonnee.IdSite = model.IdSite;
            metadonnee.IdVisibilite = model.IdVisibilite;
            metadonnee.IdTypeEnergieRenouvelable = model.IdTypeEnergieRenouvelable;
            metadonnee.SeriesTemporelles = model.SeriesTemporelles;
            metadonnee.AutoriserApi = model.AutoriserApi;
            metadonnee.Anonymiser = model.Anonymiser;
            metadonnee.AutoriserLeTelechargement = model.AutoriserLeTelechargement;
            metadonnee.DernierMiseAJour = DateTime.Now;

            var existingLinks = _context.Metadonnee_Appareil.Where(a => a.IdMetadonnee == metadonnee.Id);
            _context.Metadonnee_Appareil.RemoveRange(existingLinks);

            if (model.AppareilInfos != null)
            {
                foreach (var info in model.AppareilInfos)
                {
                    var link = new Metadonnee_Appareil
                    {
                        IdMetadonnee = metadonnee.Id,
                        IdAppareil = info.IdAppareil,
                        IdAppareilDansDonnees = info.IdAppareilDansDonnees?.Trim() ?? string.Empty,
                        Commentaire = info.Commentaire?.Trim() ?? string.Empty
                    };
                    _context.Metadonnee_Appareil.Add(link);
                }
            }

            _context.Update(metadonnee);
            _context.SaveChanges();

            HttpContext.Session.SetInt32(SessionKeys.ModificationMetadonneeId, metadonnee.Id);
            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 2);

            return RedirectToAction("EditStep2", new { id = metadonnee.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SkipStep1(int id)
        {
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            HttpContext.Session.SetInt32(SessionKeys.ModificationMetadonneeId, metadonnee.Id);
            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 2);

            return RedirectToAction("EditStep2", new { id = metadonnee.Id });
        }

        [HttpGet]
        public async Task<IActionResult> EditStep2(int id)
        {
            var (resumeId, nextStep) = GetModificationWizardState();
            if (!resumeId.HasValue || resumeId.Value != id || !nextStep.HasValue || nextStep.Value < 2)
            {
                return RedirectToAction("EditStep1", new { id });
            }

            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metadonnee == null || metadonnee.Donnees == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();

            var vm = new DonneesEditStep2ViewModel
            {
                IdMetadonnee = metadonnee.Id,
                Libelle = metadonnee.Donnees.Libelle,
                Code = metadonnee.Donnees.Code,
                Description = metadonnee.Donnees.Description,
                NombreDeCapteurs = metadonnee.Donnees.NombreDeCapteurs,
                FrequenceDeCollect = metadonnee.Donnees.FrequenceDeCollect,
                StartTimestamp = metadonnee.Donnees.StartTimestamp,
                EndTimestamp = metadonnee.Donnees.EndTimestamp,
                IdQualiteDonnees = metadonnee.Donnees.IdQualiteDonnees,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStep2(DonneesEditStep2ViewModel model)
        {
            var (resumeId, nextStep) = GetModificationWizardState();
            if (!resumeId.HasValue || resumeId.Value != model.IdMetadonnee || !nextStep.HasValue || nextStep.Value < 2)
            {
                return RedirectToAction("EditStep1", new { id = model.IdMetadonnee });
            }

            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .FirstOrDefaultAsync(m => m.Id == model.IdMetadonnee);

            if (metadonnee == null || metadonnee.Donnees == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            model.Libelle = metadonnee.Donnees.Libelle;
            model.Code = metadonnee.Donnees.Code;
            ModelState.Remove(nameof(model.Libelle));
            ModelState.Remove(nameof(model.Code));

            if (!ModelState.IsValid)
            {
                PopulateQualiteSelections(model);
                return View(model);
            }

            metadonnee.Donnees.Description = model.Description?.Trim();
            metadonnee.Donnees.NombreDeCapteurs = model.NombreDeCapteurs;
            metadonnee.Donnees.FrequenceDeCollect = model.FrequenceDeCollect;
            metadonnee.Donnees.StartTimestamp = model.StartTimestamp;
            metadonnee.Donnees.EndTimestamp = model.EndTimestamp;
            metadonnee.Donnees.IdQualiteDonnees = model.IdQualiteDonnees!.Value;
            metadonnee.DernierMiseAJour = DateTime.Now;

            _context.Update(metadonnee.Donnees);
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 3);

            return RedirectToAction("EditStep3", new { id = metadonnee.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SkipStep2(int id)
        {
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 3);

            return RedirectToAction("EditStep3", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> EditStep3(int id)
        {
            var (resumeId, nextStep) = GetModificationWizardState();
            if (!resumeId.HasValue || resumeId.Value != id || !nextStep.HasValue || nextStep.Value < 3)
            {
                return RedirectToAction("EditStep2", new { id });
            }

            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metadonnee == null || metadonnee.Donnees == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();
            var hasEventLogs = metadonnee.DonneesEventLogs != null;
            var defaultLabel = metadonnee.Donnees.Libelle ?? metadonnee.Nom;
            var label = hasEventLogs ? metadonnee.DonneesEventLogs!.Libelle : defaultLabel;
            var code = hasEventLogs
                ? metadonnee.DonneesEventLogs!.Code
                : await GenerateNextCodeForLabelAsync(defaultLabel, ExistingLabelSource.EventLogs);

            var vm = new DonneesEventLogsEditStep3ViewModel
            {
                IdMetadonnee = metadonnee.Id,
                Libelle = label,
                Code = code,
                Description = metadonnee.DonneesEventLogs?.Description,
                StartTimestamp = metadonnee.DonneesEventLogs?.StartTimestamp ?? DateTime.Now,
                EndTimestamp = metadonnee.DonneesEventLogs?.EndTimestamp ?? DateTime.Now,
                NombreDEvents = metadonnee.DonneesEventLogs?.NombreDEvents ?? 0,
                IdQualiteDonnees = metadonnee.DonneesEventLogs?.IdQualiteDonnees,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions,
                CanUploadFiles = !hasEventLogs
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStep3(DonneesEventLogsEditStep3ViewModel model)
        {
            var (resumeId, nextStep) = GetModificationWizardState();
            if (!resumeId.HasValue || resumeId.Value != model.IdMetadonnee || !nextStep.HasValue || nextStep.Value < 3)
            {
                return RedirectToAction("EditStep2", new { id = model.IdMetadonnee });
            }

            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .FirstOrDefaultAsync(m => m.Id == model.IdMetadonnee);

            if (metadonnee == null || metadonnee.Donnees == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();
            model.QualiteOptions = qualiteData.Options;
            model.QualiteDescriptions = qualiteData.Descriptions;

            var hasEventLogs = metadonnee.DonneesEventLogs != null;
            var defaultLabel = metadonnee.Donnees.Libelle ?? metadonnee.Nom;
            model.Libelle = hasEventLogs ? metadonnee.DonneesEventLogs!.Libelle : defaultLabel;
            model.Code = hasEventLogs
                ? metadonnee.DonneesEventLogs!.Code
                : await GenerateNextCodeForLabelAsync(defaultLabel, ExistingLabelSource.EventLogs);

            ModelState.Remove(nameof(model.Libelle));
            ModelState.Remove(nameof(model.Code));

            if (hasEventLogs && model.UploadedFiles != null && model.UploadedFiles.Any())
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "Event logs already exist, so new files cannot be uploaded.");
            }

            if (!ModelState.IsValid)
            {
                model.CanUploadFiles = !hasEventLogs;
                return View(model);
            }

            if (hasEventLogs)
            {
                metadonnee.DonneesEventLogs!.Description = model.Description?.Trim();
                metadonnee.DonneesEventLogs.StartTimestamp = model.StartTimestamp;
                metadonnee.DonneesEventLogs.EndTimestamp = model.EndTimestamp;
                metadonnee.DonneesEventLogs.NombreDEvents = model.NombreDEvents;
                metadonnee.DonneesEventLogs.IdQualiteDonnees = model.IdQualiteDonnees!.Value;
                metadonnee.DernierMiseAJour = DateTime.Now;

                _context.Update(metadonnee.DonneesEventLogs);
                _context.Update(metadonnee);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 4);
                return RedirectToAction("EditStep4", new { id = model.IdMetadonnee });
            }

            if ((model.UploadedFiles == null || !model.UploadedFiles.Any()) && string.IsNullOrWhiteSpace(model.UploadSessionId))
            {
                HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 4);
                return RedirectToAction("EditStep4", new { id = model.IdMetadonnee });
            }

            var hasNewUploads = model.UploadedFiles != null && model.UploadedFiles.Any();
            if (hasNewUploads)
            {
                model.ColumnTypesConfirmed = false;
                model.ColumnTypes?.Clear();
                model.ImportErrors?.Clear();
                model.ProceedAfterImportErrors = false;
            }

            UploadSession? uploadSession;
            try
            {
                uploadSession = await EnsureUploadSessionAsync(model.UploadedFiles, model.UploadSessionId, false);
                if (uploadSession == null)
                {
                    HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 4);
                    return RedirectToAction("EditStep4", new { id = model.IdMetadonnee });
                }

                model.UploadSessionId = uploadSession.SessionId;
                model.PersistedFiles = BuildPersistedFileSummaries(uploadSession);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                model.CanUploadFiles = true;
                return View(model);
            }

            if (uploadSession.TotalSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                model.CanUploadFiles = true;
                return View(model);
            }

            if (!model.ColumnTypesConfirmed)
            {
                var inferredColumns = await _fileImporter.InferColumnsAsync(uploadSession.Files);
                model.ColumnTypes = inferredColumns
                    .Select(c => new ColumnTypeSelectionViewModel
                    {
                        ColumnName = c.Name,
                        SelectedType = c.ColumnType.ToString(),
                        InferredType = c.ColumnType.ToString(),
                        InferredLength = c.MaxLength,
                        MaxLength = c.MaxLength
                    })
                    .ToList();

                model.ColumnTypesConfirmed = true;
                model.CanUploadFiles = true;
                ModelState.Clear();
                return View(model);
            }

            var baseTableName = BuildBaseTableName(model.Libelle, model.Code);
            var tableName = BuildSchemaQualifiedName(TableImportSchemas.DonneesEventLogs, baseTableName);
            var target = new TableImportTarget(TableImportSchemas.DonneesEventLogs, baseTableName);
            var selectedColumns = BuildSelectedColumns(model.ColumnTypes);
            TabularImportResult importResult;
            try
            {
                await _fileImporter.DropTableAsync(target);
                importResult = await _fileImporter.ImportAsync(target, uploadSession.Files, selectedColumns);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                model.CanUploadFiles = true;
                return View(model);
            }
            catch (SqlException)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An error occurred while importing the data.");
                model.CanUploadFiles = true;
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An unexpected error occurred while importing the data.");
                model.CanUploadFiles = true;
                return View(model);
            }

            if (importResult.Errors.Any() && !model.ProceedAfterImportErrors)
            {
                model.ImportErrors = importResult.Errors;
                model.ProceedAfterImportErrors = false;
                model.CanUploadFiles = true;
                ModelState.Clear();
                return View(model);
            }

            var eventLogs = new DonneesEventLogs
            {
                Libelle = model.Libelle.Trim(),
                Code = model.Code.Trim(),
                NomDeLaTable = tableName,
                Description = model.Description?.Trim(),
                DateAjouter = DateTime.Now,
                StartTimestamp = model.StartTimestamp,
                EndTimestamp = model.EndTimestamp,
                NombreDEvents = model.NombreDEvents,
                IdMetadonnee = metadonnee.Id,
                IdQualiteDonnees = model.IdQualiteDonnees!.Value
            };

            _context.DonneesEventLogs.Add(eventLogs);
            await _context.SaveChangesAsync();

            metadonnee.IdDonneesEventLogs = eventLogs.Id;
            metadonnee.TailleDesDonnees = FormatDataSize(ParseFormattedDataSize(metadonnee.TailleDesDonnees) + uploadSession.TotalSize);
            metadonnee.DernierMiseAJour = DateTime.Now;
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 4);

            DeleteUploadSession(model.UploadSessionId);
            model.UploadSessionId = null;

            return RedirectToAction("EditStep4", new { id = model.IdMetadonnee });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SkipStep3(int id)
        {
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 4);

            return RedirectToAction("EditStep4", new { id });
        }

        [HttpGet]
        public async Task<IActionResult> EditStep4(int id)
        {
            var (resumeId, nextStep) = GetModificationWizardState();
            if (!resumeId.HasValue || resumeId.Value != id || !nextStep.HasValue || nextStep.Value < 4)
            {
                return RedirectToAction("EditStep3", new { id });
            }

            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metadonnee == null || metadonnee.Donnees == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();
            var hasContext = metadonnee.DonneesContexteEnvironnemental != null;
            var defaultLabel = metadonnee.Donnees.Libelle ?? metadonnee.Nom;
            var label = hasContext ? metadonnee.DonneesContexteEnvironnemental!.Libelle : defaultLabel;
            var code = hasContext
                ? metadonnee.DonneesContexteEnvironnemental!.Code
                : await GenerateNextCodeForLabelAsync(defaultLabel, ExistingLabelSource.Contexte);

            var vm = new DonneesContexteEditStep4ViewModel
            {
                IdMetadonnee = metadonnee.Id,
                Libelle = label,
                Code = code,
                Description = metadonnee.DonneesContexteEnvironnemental?.Description,
                StartTimestamp = metadonnee.DonneesContexteEnvironnemental?.StartTimestamp ?? DateTime.Now,
                EndTimestamp = metadonnee.DonneesContexteEnvironnemental?.EndTimestamp ?? DateTime.Now,
                IdQualiteDonnees = metadonnee.DonneesContexteEnvironnemental?.IdQualiteDonnees,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions,
                CanUploadFiles = !hasContext
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditStep4(DonneesContexteEditStep4ViewModel model)
        {
            var (resumeId, nextStep) = GetModificationWizardState();
            if (!resumeId.HasValue || resumeId.Value != model.IdMetadonnee || !nextStep.HasValue || nextStep.Value < 4)
            {
                return RedirectToAction("EditStep3", new { id = model.IdMetadonnee });
            }

            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m => m.Id == model.IdMetadonnee);

            if (metadonnee == null || metadonnee.Donnees == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();
            model.QualiteOptions = qualiteData.Options;
            model.QualiteDescriptions = qualiteData.Descriptions;

            var hasContext = metadonnee.DonneesContexteEnvironnemental != null;
            var defaultLabel = metadonnee.Donnees.Libelle ?? metadonnee.Nom;
            model.Libelle = hasContext ? metadonnee.DonneesContexteEnvironnemental!.Libelle : defaultLabel;
            model.Code = hasContext
                ? metadonnee.DonneesContexteEnvironnemental!.Code
                : await GenerateNextCodeForLabelAsync(defaultLabel, ExistingLabelSource.Contexte);

            ModelState.Remove(nameof(model.Libelle));
            ModelState.Remove(nameof(model.Code));

            if (hasContext && model.UploadedFiles != null && model.UploadedFiles.Any())
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "Environmental context already exists, so new files cannot be uploaded.");
            }

            if (!ModelState.IsValid)
            {
                model.CanUploadFiles = !hasContext;
                return View(model);
            }

            if (hasContext)
            {
                metadonnee.DonneesContexteEnvironnemental!.Description = model.Description?.Trim();
                metadonnee.DonneesContexteEnvironnemental.StartTimestamp = model.StartTimestamp;
                metadonnee.DonneesContexteEnvironnemental.EndTimestamp = model.EndTimestamp;
                metadonnee.DonneesContexteEnvironnemental.IdQualiteDonnees = model.IdQualiteDonnees!.Value;
                metadonnee.DernierMiseAJour = DateTime.Now;

                _context.Update(metadonnee.DonneesContexteEnvironnemental);
                _context.Update(metadonnee);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 5);
                return RedirectToAction("Details", "Donnees", new { id = model.IdMetadonnee, modification = true });
            }

            if ((model.UploadedFiles == null || !model.UploadedFiles.Any()) && string.IsNullOrWhiteSpace(model.UploadSessionId))
            {
                HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 5);
                return RedirectToAction("Details", "Donnees", new { id = model.IdMetadonnee, modification = true });
            }

            var hasNewUploads = model.UploadedFiles != null && model.UploadedFiles.Any();
            if (hasNewUploads)
            {
                model.ColumnTypesConfirmed = false;
                model.ColumnTypes?.Clear();
                model.ImportErrors?.Clear();
                model.ProceedAfterImportErrors = false;
            }

            UploadSession? uploadSession;
            try
            {
                uploadSession = await EnsureUploadSessionAsync(model.UploadedFiles, model.UploadSessionId, false);
                if (uploadSession == null)
                {
                    HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 5);
                    return RedirectToAction("Details", "Donnees", new { id = model.IdMetadonnee, modification = true });
                }

                model.UploadSessionId = uploadSession.SessionId;
                model.PersistedFiles = BuildPersistedFileSummaries(uploadSession);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                model.CanUploadFiles = true;
                return View(model);
            }

            if (uploadSession.TotalSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                model.CanUploadFiles = true;
                return View(model);
            }

            if (!model.ColumnTypesConfirmed)
            {
                var inferredColumns = await _fileImporter.InferColumnsAsync(uploadSession.Files);
                model.ColumnTypes = inferredColumns
                    .Select(c => new ColumnTypeSelectionViewModel
                    {
                        ColumnName = c.Name,
                        SelectedType = c.ColumnType.ToString(),
                        InferredType = c.ColumnType.ToString(),
                        InferredLength = c.MaxLength,
                        MaxLength = c.MaxLength
                    })
                    .ToList();

                model.ColumnTypesConfirmed = true;
                model.CanUploadFiles = true;
                ModelState.Clear();
                return View(model);
            }

            var baseTableName = BuildBaseTableName(model.Libelle, model.Code);
            var tableName = BuildSchemaQualifiedName(TableImportSchemas.DonneesContexteEnvironnemental, baseTableName);
            var target = new TableImportTarget(TableImportSchemas.DonneesContexteEnvironnemental, baseTableName);
            var selectedColumns = BuildSelectedColumns(model.ColumnTypes);
            TabularImportResult importResult;
            try
            {
                await _fileImporter.DropTableAsync(target);
                importResult = await _fileImporter.ImportAsync(target, uploadSession.Files, selectedColumns);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                model.CanUploadFiles = true;
                return View(model);
            }
            catch (SqlException)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An error occurred while importing the data.");
                model.CanUploadFiles = true;
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An unexpected error occurred while importing the data.");
                model.CanUploadFiles = true;
                return View(model);
            }

            if (importResult.Errors.Any() && !model.ProceedAfterImportErrors)
            {
                model.ImportErrors = importResult.Errors;
                model.ProceedAfterImportErrors = false;
                model.CanUploadFiles = true;
                ModelState.Clear();
                return View(model);
            }

            var donneesContext = new DonneesContexteEnvironnemental
            {
                Libelle = model.Libelle.Trim(),
                Code = model.Code.Trim(),
                NomDeLaTable = tableName,
                Description = model.Description?.Trim(),
                DateAjouter = DateTime.Now,
                StartTimestamp = model.StartTimestamp,
                EndTimestamp = model.EndTimestamp,
                IdMetadonnee = metadonnee.Id,
                IdQualiteDonnees = model.IdQualiteDonnees!.Value
            };

            _context.DonneesContexteEnvironnemental.Add(donneesContext);
            await _context.SaveChangesAsync();

            metadonnee.IdDonneesContexteEnvironnemental = donneesContext.Id;
            metadonnee.TailleDesDonnees = FormatDataSize(ParseFormattedDataSize(metadonnee.TailleDesDonnees) + uploadSession.TotalSize);
            metadonnee.DernierMiseAJour = DateTime.Now;
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 5);

            DeleteUploadSession(model.UploadSessionId);
            model.UploadSessionId = null;

            return RedirectToAction("Details", "Donnees", new { id = model.IdMetadonnee, modification = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SkipStep4(int id)
        {
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!CanEditMetadonnee(metadonnee))
            {
                return Forbid();
            }

            HttpContext.Session.SetInt32(SessionKeys.ModificationNextStep, 5);

            return RedirectToAction("Details", "Donnees", new { id, modification = true });
        }

        private void PopulateStep1Selections(DonneesEditStep1ViewModel model)
        {
            var visibilites = _context.Visibilite.AsQueryable();
            if (User.IsInRole("user"))
            {
                visibilites = visibilites.Where(v => v.Id == VisibiliteIds.Personnelle);
            }

            model.Licences = _context.Licence.Where(l => l.Actif).ToList();
            model.Sites = _context.Site.Where(s => s.Actif).ToList();
            model.Visibilites = visibilites.ToList();
            model.TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList();
            model.Appareils = _context.Appareil.Where(a => a.Actif).ToList();
            model.AppareilInfos ??= new List<MetadonneeAppareilInfo>();
        }

        private void PopulateQualiteSelections(DonneesEditStep2ViewModel model)
        {
            var qualiteData = BuildQualiteOptions();
            model.QualiteOptions = qualiteData.Options;
            model.QualiteDescriptions = qualiteData.Descriptions;
        }

        private (List<SelectListItem> Options, Dictionary<string, string> Descriptions) BuildQualiteOptions()
        {
            var qualites = _context.QualiteDonnees
                .OrderBy(q => q.Libelle)
                .Select(q => new { q.Id, q.Libelle, q.Description })
                .ToList();

            var options = qualites
                .Select(q => new SelectListItem
                {
                    Value = q.Id.ToString(),
                    Text = q.Libelle
                })
                .ToList();

            options.Insert(0, new SelectListItem
            {
                Value = string.Empty,
                Text = "Select a quality"
            });

            var descriptions = qualites.ToDictionary(q => q.Id.ToString(), q => q.Description ?? string.Empty);
            descriptions[string.Empty] = string.Empty;

            return (options, descriptions);
        }

        private async Task<UploadSession?> EnsureUploadSessionAsync(ICollection<IFormFile>? uploadedFiles, string? uploadSessionId, bool requireFiles)
        {
            if (uploadedFiles != null && uploadedFiles.Any())
            {
                if (!string.IsNullOrWhiteSpace(uploadSessionId))
                {
                    DeleteUploadSession(uploadSessionId);
                }

                var sessionId = await PersistUploadsAsync(uploadedFiles);
                var persistedFiles = LoadPersistedUploads(sessionId);
                var dataSize = persistedFiles.Sum(f => f.Length);
                return new UploadSession(sessionId, persistedFiles, dataSize);
            }

            if (!string.IsNullOrWhiteSpace(uploadSessionId))
            {
                var existingFiles = LoadPersistedUploads(uploadSessionId);
                var existingSize = existingFiles.Sum(f => f.Length);
                return new UploadSession(uploadSessionId, existingFiles, existingSize);
            }

            if (requireFiles)
            {
                throw new InvalidOperationException("You must upload at least one data file (CSV, XLSX, Parquet, or CSV.zip).");
            }

            return null;
        }

        private async Task<string> PersistUploadsAsync(IEnumerable<IFormFile> files)
        {
            var sessionId = Guid.NewGuid().ToString("N");
            var sessionPath = GetUploadSessionPath(sessionId);
            Directory.CreateDirectory(sessionPath);

            var metadata = new List<PersistedFileMetadata>();

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    continue;
                }

                var safeName = Path.GetFileName(file.FileName);
                var storedName = $"{Guid.NewGuid():N}_{safeName}";
                var destination = Path.Combine(sessionPath, storedName);

                await using (var target = System.IO.File.Create(destination))
                {
                    await file.CopyToAsync(target);
                }

                metadata.Add(new PersistedFileMetadata(storedName, safeName, file.ContentType ?? "application/octet-stream"));
            }

            if (metadata.Count == 0)
            {
                throw new InvalidOperationException("You must upload at least one data file (CSV, XLSX, Parquet, or CSV.zip).");
            }

            var metadataPath = Path.Combine(sessionPath, UploadMetadataFileName);
            await System.IO.File.WriteAllTextAsync(metadataPath, SystemTextJson.Serialize(metadata));

            return sessionId;
        }

        private List<IFormFile> LoadPersistedUploads(string sessionId)
        {
            var sessionPath = GetUploadSessionPath(sessionId);
            if (!Directory.Exists(sessionPath))
            {
                throw new InvalidOperationException("The upload session has expired. Please upload your files again.");
            }

            var metadataPath = Path.Combine(sessionPath, UploadMetadataFileName);
            if (!System.IO.File.Exists(metadataPath))
            {
                throw new InvalidOperationException("The upload session metadata is missing.");
            }

            var metadataJson = System.IO.File.ReadAllText(metadataPath);
            var metadata = SystemTextJson.Deserialize<List<PersistedFileMetadata>>(metadataJson) ?? new List<PersistedFileMetadata>();

            var files = new List<IFormFile>();

            foreach (var entry in metadata)
            {
                var path = Path.Combine(sessionPath, entry.StoredName);
                if (!System.IO.File.Exists(path))
                {
                    continue;
                }

                files.Add(new PhysicalFormFile(path, entry.OriginalFileName, entry.ContentType));
            }

            if (files.Count == 0)
            {
                throw new InvalidOperationException("The upload session is empty. Please upload your files again.");
            }

            return files;
        }

        private void DeleteUploadSession(string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            var sessionPath = GetUploadSessionPath(sessionId);
            if (!Directory.Exists(sessionPath))
            {
                return;
            }

            try
            {
                Directory.Delete(sessionPath, true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }

        private static List<PersistedFileSummary> BuildPersistedFileSummaries(UploadSession session)
        {
            return session.Files
                .Select(f => new PersistedFileSummary
                {
                    Name = f.FileName ?? string.Empty,
                    SizeBytes = f.Length
                })
                .ToList();
        }

        private static List<TabularColumnDefinition> BuildSelectedColumns(IReadOnlyCollection<ColumnTypeSelectionViewModel> columnTypes)
        {
            if (columnTypes == null || columnTypes.Count == 0)
            {
                throw new InvalidOperationException("Column types are required to create the table.");
            }

            var columns = new List<TabularColumnDefinition>();
            var fallbackLength = 255;

            foreach (var column in columnTypes)
            {
                if (!Enum.TryParse<TabularColumnType>(column.SelectedType, out var columnType))
                {
                    columnType = TabularColumnType.NVarChar;
                }

                int? length = null;
                if (columnType == TabularColumnType.NVarChar)
                {
                    if (column.MaxLength.HasValue && column.MaxLength.Value > 0)
                    {
                        length = column.MaxLength.Value;
                    }
                    else if (column.InferredLength.HasValue && column.InferredLength.Value > 0)
                    {
                        length = column.InferredLength.Value;
                    }
                    else
                    {
                        length = fallbackLength;
                    }
                }

                columns.Add(new TabularColumnDefinition(column.ColumnName, columnType, length));
            }

            return columns;
        }

        private string GetUploadSessionPath(string sessionId)
        {
            return Path.Combine(Path.GetTempPath(), UploadCacheFolderName, sessionId);
        }

        private record PersistedFileMetadata(string StoredName, string OriginalFileName, string ContentType);

        private record UploadSession(string SessionId, List<IFormFile> Files, long TotalSize);

        private (int? metadonneeId, int? nextStep) GetModificationWizardState()
        {
            var metadonneeId = HttpContext.Session.GetInt32(SessionKeys.ModificationMetadonneeId);
            var nextStep = HttpContext.Session.GetInt32(SessionKeys.ModificationNextStep);

            return (metadonneeId, nextStep);
        }

        private int? TryGetCurrentUserId()
        {
            return HttpContextUserHelper.TryGetCurrentUserId(User);
        }

        private bool CanEditMetadonnee(Metadonnee metadonnee)
        {
            if (User.IsInRole("administrator"))
            {
                return true;
            }

            var userId = TryGetCurrentUserId();
            return userId.HasValue && metadonnee.IdUtilisateur == userId.Value;
        }

        private static int ExtractSuffixNumber(string code)
        {
            var match = Regex.Match(code ?? string.Empty, @"(\d{2})(?!.*\d)");
            return match.Success && int.TryParse(match.Value, out var number) ? number : 0;
        }

        private IQueryable<string> BuildCodesQueryForLabel(string normalizedLibelle, ExistingLabelSource source)
        {
            return source switch
            {
                ExistingLabelSource.EventLogs => _context.DonneesEventLogs
                    .Where(e => e.Libelle.ToLower() == normalizedLibelle && e.Code != null)
                    .Select(e => e.Code!),
                ExistingLabelSource.Contexte => _context.DonneesContexteEnvironnemental
                    .Where(c => c.Libelle.ToLower() == normalizedLibelle && c.Code != null)
                    .Select(c => c.Code!),
                _ => Enumerable.Empty<string>().AsQueryable()
            };
        }

        private async Task<string> GenerateNextCodeForLabelAsync(string libelle, ExistingLabelSource source)
        {
            var datePrefix = DateTime.Now.ToString("yyyyMMdd");

            if (string.IsNullOrWhiteSpace(libelle))
            {
                return $"{datePrefix}01";
            }

            var normalizedLibelle = libelle.Trim().ToLower();
            var codes = await BuildCodesQueryForLabel(normalizedLibelle, source).ToListAsync();
            var nextNumber = codes.Select(ExtractSuffixNumber).DefaultIfEmpty(0).Max() + 1;

            return $"{datePrefix}{nextNumber:D2}";
        }

        private static string BuildBaseTableName(string libelle, string code)
        {
            return $"{libelle}-{code}".Replace(" ", "_");
        }

        private static string BuildSchemaQualifiedName(string schema, string tableName)
        {
            return $"{schema}.{tableName}";
        }

        private static string FormatDataSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:F1} KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return $"{mb:F1} MB";
            double gb = mb / 1024.0;
            return $"{gb:F1} GB";
        }

        private static long ParseFormattedDataSize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return 0;
            }

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var size) &&
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out size))
            {
                return 0;
            }

            var unit = parts[1].Trim().ToUpperInvariant();
            return unit switch
            {
                "B" => (long)size,
                "KB" => (long)(size * 1024d),
                "MB" => (long)(size * 1024d * 1024d),
                "GB" => (long)(size * 1024d * 1024d * 1024d),
                "TB" => (long)(size * 1024d * 1024d * 1024d * 1024d),
                _ => 0
            };
        }
    }
}
