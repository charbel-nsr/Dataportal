using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Dataportal.Classes;
using Dataportal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;

namespace Dataportal.Controllers
{
    public class DonneesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITabularFileImporter _fileImporter;
        private const string DataSizeTempDataKey = "DataSizeBytes";

        public DonneesController(ApplicationDbContext context, ITabularFileImporter fileImporter)
        {
            _context = context;
            _fileImporter = fileImporter;
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public IActionResult CreateStep1()
        {
            var visibilites = _context.Visibilite.AsQueryable();
            if (User.IsInRole("user"))
            {
                visibilites = visibilites.Where(v => v.Id == VisibiliteIds.Personnelle);
            }

            var vm = new MetadonneeCreateViewModel
            {
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                Sites = _context.Site.Where(s => s.Actif).ToList(),
                Visibilites = visibilites.ToList(),
                TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList(),
                Appareils = _context.Appareil.Where(a => a.Actif).ToList(),
                AppareilInfos = new List<MetadonneeAppareilInfo>()
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public IActionResult CreateStep1(MetadonneeCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload choices
                model.Licences = _context.Licence.Where(l => l.Actif).ToList();
                model.Sites = _context.Site.Where(s => s.Actif).ToList();
                var visibilites = _context.Visibilite.AsQueryable();
                if (User.IsInRole("user"))
                {
                    visibilites = visibilites.Where(v => v.Id == VisibiliteIds.Personnelle);
                }
                model.Visibilites = visibilites.ToList();
                model.TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList();
                model.Appareils = _context.Appareil.Where(a => a.Actif).ToList();
                model.AppareilInfos ??= new List<MetadonneeAppareilInfo>();
                return View(model);
            }

            if (User.IsInRole("user") && model.IdVisibilite != VisibiliteIds.Personnelle)
            {
                ModelState.AddModelError("IdVisibilite", "Standard users can only create personal data.");
                // Reload choices
                var visibilites = _context.Visibilite.Where(v => v.Id == VisibiliteIds.Personnelle).ToList();
                model.Licences = _context.Licence.Where(l => l.Actif).ToList();
                model.Sites = _context.Site.Where(s => s.Actif).ToList();
                model.Visibilites = visibilites;
                model.TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList();
                model.Appareils = _context.Appareil.Where(a => a.Actif).ToList();
                model.AppareilInfos ??= new List<MetadonneeAppareilInfo>();
                return View(model);
            }

            TempData["Step1Data"] = JsonConvert.SerializeObject(model);

            return RedirectToAction("CreateStep2");
        }

        private int GetCurrentUserId()
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
                throw new UnauthorizedAccessException("UserId claim missing.");

            return userId.Value;
        }

        private int? TryGetCurrentUserId()
        {
            return HttpContextUserHelper.TryGetCurrentUserId(User);
        }

        /// <summary>
        /// Retrieves the dataset creation wizard state from the session.
        /// <para>
        /// <list type="bullet">
        /// <item>The returned <c>metadonneeId</c> identifies the dataset that can be resumed.</item>
        /// <item>The returned <c>nextStep</c> is the next stage the user should see; completed steps redirect to the summary page.</item>
        /// </list>
        /// </para>
        /// </summary>
        private (int? metadonneeId, int? nextStep) GetCreationWizardState()
        {
            var metadonneeId = HttpContext.Session.GetInt32(SessionKeys.CreationMetadonneeId);
            var nextStep = HttpContext.Session.GetInt32(SessionKeys.CreationNextStep);

            return (metadonneeId, nextStep);
        }

        private (List<SelectListItem> Options, Dictionary<string, string> Descriptions) BuildQualiteOptions()
        {
            var qualites = _context.QualiteDonnees
                .OrderBy(q => q.Libelle)
                .Select(q => new
                {
                    q.Id,
                    q.Libelle,
                    q.Description
                })
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

            var descriptions = qualites.ToDictionary(
                q => q.Id.ToString(),
                q => q.Description ?? string.Empty);

            descriptions[string.Empty] = string.Empty;

            return (options, descriptions);
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public IActionResult CreateStep2()
        {
            var (resumeId, nextStep) = GetCreationWizardState();

            if (resumeId.HasValue && nextStep.HasValue && nextStep.Value >= 3)
            {
                return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
            }

            var step1Json = TempData.Peek("Step1Data") as string;
            if (string.IsNullOrEmpty(step1Json))
            {
                if (resumeId.HasValue)
                {
                    return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
                }

                TempData["Error"] = "You must complete the first step first.";
                return RedirectToAction("CreateStep1");
            }

            // show the upload + Donnees form
            var qualiteData = BuildQualiteOptions();
            var vm = new DonneesCreateStep2ViewModel
            {
                StartTimestamp = DateTime.Now,
                EndTimestamp = DateTime.Now,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep2(DonneesCreateStep2ViewModel model)
        {
            var step1Json = TempData["Step1Data"] as string;
            if (string.IsNullOrEmpty(step1Json))
            {
                TempData["Error"] = "The information from the first step is missing.";
                return RedirectToAction("CreateStep1");
            }

            var step1Data = JsonConvert.DeserializeObject<MetadonneeCreateViewModel>(step1Json);

            void RepopulateQualiteSelections()
            {
                var qualiteDataLocal = BuildQualiteOptions();
                model.QualiteOptions = qualiteDataLocal.Options;
                model.QualiteDescriptions = qualiteDataLocal.Descriptions;
            }

            if (User.IsInRole("user") && step1Data.IdVisibilite != VisibiliteIds.Personnelle)
            {
                TempData.Remove("Step1Data");
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            var normalizedLibelle = model.Libelle.Trim().ToLower();
            var normalizedCode = model.Code.Trim().ToLower();

            var duplicate = await _context.Donnees
                .FirstOrDefaultAsync(d =>
                    d.Libelle.ToLower() == normalizedLibelle &&
                    d.Code.ToLower() == normalizedCode);

            if (duplicate != null)
            {
                ModelState.AddModelError("Libelle", "This label/code combination already exists.");
                ModelState.AddModelError("Code", "This label/code combination already exists.");

                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            if (model.UploadedFiles == null || !model.UploadedFiles.Any())
            {
                ModelState.AddModelError("UploadedFiles", "You must upload at least one data file (CSV, XLSX, Parquet, or CSV.zip).");
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            // Calculate uploaded data size and store for next steps
            long dataSize = model.UploadedFiles.Sum(f => f.Length);
            if (dataSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            // -- Show optional processing page here if you want
            // return View("Processing");

            var tableName = $"Donnees.{model.Libelle}-{model.Code}".Replace(" ", "_");
            try
            {
                await _fileImporter.ImportAsync(tableName, model.UploadedFiles);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }
            catch (SqlException)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An error occurred while importing the data.");
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An unexpected error occurred while importing the data.");
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            TempData[DataSizeTempDataKey] = dataSize.ToString(CultureInfo.InvariantCulture);

            // Create Donnees
            var donnees = new Donnees
            {
                Libelle = model.Libelle.Trim(),
                Code = model.Code.Trim(),
                NomDeLaTable = tableName,
                Description = model.Description?.Trim(),
                NombreDeCapteurs = model.NombreDeCapteurs,
                FrequenceDeCollect = model.FrequenceDeCollect,
                DateAjouter = DateTime.Now,
                StartTimestamp = model.StartTimestamp,
                EndTimestamp = model.EndTimestamp,
                IdQualiteDonnees = model.IdQualiteDonnees!.Value
            };

            _context.Donnees.Add(donnees);
            await _context.SaveChangesAsync();

            // Create Metadonnee
            var metadonnee = new Metadonnee
            {
                Nom = step1Data.Nom.Trim(),
                Description = step1Data.Description.Trim(),
                IdLicence = step1Data.IdLicence,
                IdSite = step1Data.IdSite,
                IdVisibilite = step1Data.IdVisibilite,
                IdTypeEnergieRenouvelable = step1Data.IdTypeEnergieRenouvelable,
                TailleDesDonnees = FormatDataSize(dataSize),
                SeriesTemporelles = step1Data.SeriesTemporelles,
                AutoriserApi = step1Data.AutoriserApi,
                Anonymiser = step1Data.Anonymiser,
                AutoriserLeTelechargement = step1Data.AutoriserLeTelechargement,
                IdUtilisateur = GetCurrentUserId(),
                TraitementEnCours = false,
                DernierMiseAJour = DateTime.Now,
                NombreDeTelechargements = 0,
                QualiteDesDonnees = 0,
                IdDonnees = donnees.Id
            };

            _context.Metadonnee.Add(metadonnee);
            await _context.SaveChangesAsync();

            donnees.IdMetadonnee = metadonnee.Id;
            _context.Donnees.Update(donnees);

            if (step1Data.AppareilInfos != null)
            {
                foreach (var info in step1Data.AppareilInfos)
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

            await _context.SaveChangesAsync();

            // Clear Step1Data from TempData
            TempData.Remove("Step1Data");

            HttpContext.Session.SetInt32(SessionKeys.CreationMetadonneeId, metadonnee.Id);
            HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 3);

            return RedirectToAction("CreateStep3", new { id = metadonnee.Id });
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public IActionResult CreateStep3(int id)
        {
            var (resumeId, nextStep) = GetCreationWizardState();

            if (resumeId.HasValue)
            {
                if (resumeId.Value != id || (nextStep.HasValue && nextStep.Value > 3))
                {
                    return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
                }
            }

            // Validate Metadonnee exists
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();
            var vm = new DonneesEventLogsCreateStep3ViewModel
            {
                IdMetadonnee = id,
                StartTimestamp = DateTime.Now,
                EndTimestamp = DateTime.Now,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep3(DonneesEventLogsCreateStep3ViewModel model)
        {
            void RepopulateQualiteSelections()
            {
                var qualiteDataLocal = BuildQualiteOptions();
                model.QualiteOptions = qualiteDataLocal.Options;
                model.QualiteDescriptions = qualiteDataLocal.Descriptions;
            }

            if (!ModelState.IsValid)
            {
                RepopulateQualiteSelections();
                return View(model);
            }

            var metadonnee = await _context.Metadonnee.FindAsync(model.IdMetadonnee);
            if (metadonnee == null)
            {
                TempData["Error"] = "Metadata not found.";
                return RedirectToAction("CreateStep1");
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            // Handle skip: if no file uploaded, user wants to skip
            if (model.UploadedFiles == null || !model.UploadedFiles.Any())
            {
                HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 4);
                return RedirectToAction("CreateStep4", new { id = model.IdMetadonnee });
            }

            var duplicate = await _context.DonneesEventLogs.FirstOrDefaultAsync(e =>
                            e.Libelle.ToLower() == model.Libelle.Trim().ToLower() ||
                            e.Code.ToLower() == model.Code.Trim().ToLower());

            if (duplicate != null)
            {
                if (duplicate.Libelle.Equals(model.Libelle.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Libelle", "This label already exists.");
                if (duplicate.Code.Equals(model.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Code", "This code already exists.");

                RepopulateQualiteSelections();
                return View(model);
            }

            // Update data size with uploaded files
            long existingDataSize = 0;
            var sizeValue = TempData.Peek(DataSizeTempDataKey);
            if (sizeValue != null)
            {
                existingDataSize = ParseDataSize(sizeValue);
            }
            long stepSize = model.UploadedFiles.Sum(f => f.Length);
            if (stepSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                RepopulateQualiteSelections();
                return View(model);
            }
            long totalDataSize = existingDataSize + stepSize;

            var tableName = $"DonneesEventLogs.{model.Libelle}-{model.Code}".Replace(" ", "_");
            try
            {
                await _fileImporter.ImportAsync(tableName, model.UploadedFiles);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                RepopulateQualiteSelections();
                return View(model);
            }
            catch (SqlException)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An error occurred while importing the data.");
                RepopulateQualiteSelections();
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An unexpected error occurred while importing the data.");
                RepopulateQualiteSelections();
                return View(model);
            }

            // Create DonneesEventLogs
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
                IdMetadonnee = model.IdMetadonnee,
                IdQualiteDonnees = model.IdQualiteDonnees!.Value
            };

            _context.DonneesEventLogs.Add(eventLogs);
            await _context.SaveChangesAsync();

            // Link to Metadonnee and update data size
            metadonnee.IdDonneesEventLogs = eventLogs.Id;
            metadonnee.TailleDesDonnees = FormatDataSize(totalDataSize);
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            TempData[DataSizeTempDataKey] = totalDataSize.ToString(CultureInfo.InvariantCulture);

            HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 4);

            return RedirectToAction("CreateStep4", new { id = model.IdMetadonnee });
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public IActionResult SkipStep3(int id)
        {
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 4);

            return RedirectToAction("CreateStep4", new { id });
        }

        // GET: /Donnees/CreateStep4/{id}
        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public IActionResult CreateStep4(int id)
        {
            var (resumeId, nextStep) = GetCreationWizardState();

            if (resumeId.HasValue)
            {
                if (resumeId.Value != id)
                {
                    return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
                }

                if (nextStep.HasValue)
                {
                    if (nextStep.Value < 4)
                    {
                        return RedirectToAction("CreateStep3", new { id = resumeId.Value });
                    }

                    if (nextStep.Value > 4)
                    {
                        return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
                    }
                }
            }

            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            var qualiteData = BuildQualiteOptions();
            var vm = new DonneesContexteEnvironnementalCreateStep4ViewModel
            {
                IdMetadonnee = id,
                StartTimestamp = DateTime.Now,
                EndTimestamp = DateTime.Now,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions
            };
            return View(vm);
        }

        // POST: /Donnees/CreateStep4
        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep4(DonneesContexteEnvironnementalCreateStep4ViewModel model)
        {
            void RepopulateQualiteSelections()
            {
                var qualiteDataLocal = BuildQualiteOptions();
                model.QualiteOptions = qualiteDataLocal.Options;
                model.QualiteDescriptions = qualiteDataLocal.Descriptions;
            }

            if (!ModelState.IsValid)
            {
                RepopulateQualiteSelections();
                return View(model);
            }

            var metadonnee = await _context.Metadonnee.FindAsync(model.IdMetadonnee);
            if (metadonnee == null)
            {
                TempData["Error"] = "Metadata not found.";
                return RedirectToAction("CreateStep1");
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            if (model.UploadedFiles == null || !model.UploadedFiles.Any())
            {
                // User skipped uploading: go straight to next step
                HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 5);
                TempData.Remove(DataSizeTempDataKey);
                return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
            }

            var duplicate = await _context.DonneesContexteEnvironnemental.FirstOrDefaultAsync(c =>
                            c.Libelle.ToLower() == model.Libelle.Trim().ToLower() ||
                            c.Code.ToLower() == model.Code.Trim().ToLower());

            if (duplicate != null)
            {
                if (duplicate.Libelle.Equals(model.Libelle.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Libelle", "This label already exists.");
                if (duplicate.Code.Equals(model.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Code", "This code already exists.");

                RepopulateQualiteSelections();
                return View(model);
            }

            // Update data size with uploaded files
            long existingDataSize = 0;
            var sizeValue = TempData.Peek(DataSizeTempDataKey);
            if (sizeValue != null)
            {
                existingDataSize = ParseDataSize(sizeValue);
            }
            long stepSize = model.UploadedFiles.Sum(f => f.Length);
            if (stepSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                RepopulateQualiteSelections();
                return View(model);
            }
            long totalDataSize = existingDataSize + stepSize;

            var tableName = $"DonneesContexteEnvironnemental.{model.Libelle}-{model.Code}".Replace(" ", "_");
            try
            {
                await _fileImporter.ImportAsync(tableName, model.UploadedFiles);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                RepopulateQualiteSelections();
                return View(model);
            }
            catch (SqlException)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An error occurred while importing the data.");
                RepopulateQualiteSelections();
                return View(model);
            }
            catch (Exception)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), "An unexpected error occurred while importing the data.");
                RepopulateQualiteSelections();
                return View(model);
            }

            // Save DonneesContexteEnvironnemental
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

            // Link to Metadonnee and update data size
            metadonnee.IdDonneesContexteEnvironnemental = donneesContext.Id;
            metadonnee.TailleDesDonnees = FormatDataSize(totalDataSize);
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();
            TempData.Remove(DataSizeTempDataKey);

            HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 5);

            // Proceed to next step
            return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id, bool? creation)
        {
            // 1️⃣ Load Metadonnee + navigation
            var metadonnee = await _context.Metadonnee
                .Include(m => m.Licence)
                .Include(m => m.Site)
                .Include(m => m.Visibilite)
                .Include(m => m.TypeEnergieRenouvelable)
                .Include(m => m.Utilisateur).ThenInclude(u => u.Entreprise)
                .Include(m => m.Metadonnee_Appareils).ThenInclude(ma => ma.Appareil)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metadonnee == null)
                return NotFound();

            if (!CanCurrentUserAccessMetadonnee(metadonnee, out var requiresAuthentication))
            {
                return requiresAuthentication ? Challenge() : Forbid();
            }

            var resumeId = HttpContext.Session.GetInt32(SessionKeys.CreationMetadonneeId);
            if (creation == true && resumeId.HasValue && resumeId.Value == metadonnee.Id)
            {
                // The summary view has been reached for the tracked creation; clear the session markers.
                HttpContext.Session.Remove(SessionKeys.CreationMetadonneeId);
                HttpContext.Session.Remove(SessionKeys.CreationNextStep);
            }

            //Load Donnees / EventLogs / Contexte
            var donnees = await _context.Donnees
                .Include(d => d.QualiteDonnees)
                .FirstOrDefaultAsync(d => d.Id == metadonnee.IdDonnees);
            var eventLogs = await _context.DonneesEventLogs
                .Include(e => e.QualiteDonnees)
                .FirstOrDefaultAsync(e => e.Id == metadonnee.IdDonneesEventLogs);
            var contexte = await _context.DonneesContexteEnvironnemental
                .Include(c => c.QualiteDonnees)
                .FirstOrDefaultAsync(c => c.Id == metadonnee.IdDonneesContexteEnvironnemental);

            if (creation == true)
            {
                ViewData["CurrentStep"] = 5;
            }

            //Load preview data
            var donneesPreview = donnees?.NomDeLaTable != null ? await GetTablePreviewRows(donnees.NomDeLaTable) : null;
            var eventLogsPreview = eventLogs?.NomDeLaTable != null ? await GetTablePreviewRows(eventLogs.NomDeLaTable) : null;
            var contextePreview = contexte?.NomDeLaTable != null ? await GetTablePreviewRows(contexte.NomDeLaTable) : null;

            //Build ViewModel
            var vm = new MetadonneeDetailsViewModel
            {
                Metadonnee = metadonnee,
                Licence = metadonnee.Licence,
                Site = metadonnee.Site,
                Visibilite = metadonnee.Visibilite,
                Utilisateur = metadonnee.Utilisateur,
                TypeEnergieRenouvelable = metadonnee.TypeEnergieRenouvelable,
                AppareilsLies = metadonnee.Metadonnee_Appareils?.ToList(),

                Donnees = donnees,
                DonneesPreviewRows = donneesPreview,

                DonneesEventLogs = eventLogs,
                EventLogsPreviewRows = eventLogsPreview,

                DonneesContexteEnvironnemental = contexte,
                ContextePreviewRows = contextePreview
            };

            return View(vm);
        }

        private async Task<List<Dictionary<string, object>>> GetTablePreviewRows(string tableName)
        {
            var results = new List<Dictionary<string, object>>();

            if (string.IsNullOrWhiteSpace(tableName))
                return results;

            // Sanitize table name for SQL Server
            var safeTableName = $"[{tableName.Replace("]", "]]")}]";

            using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var query = $"SELECT TOP 10 * FROM {safeTableName}";
            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i)?.ToString() ?? "";
                }
                results.Add(row);
            }

            return results;
        }

        private int? GetCurrentUserRole()
        {
            return HttpContextUserHelper.GetCurrentUserRole(HttpContext, _context);
        }

        private int? GetCurrentUserEntrepriseId()
        {
            return HttpContextUserHelper.GetCurrentUserEntrepriseId(HttpContext, _context);
        }

        private bool CanCurrentUserAccessMetadonnee(Metadonnee metadonnee, out bool requiresAuthentication)
        {
            return HttpContextUserHelper.CanCurrentUserAccessMetadonnee(HttpContext, _context, metadonnee, out requiresAuthentication);
        }


        private async Task DropSqlTableIfExists(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return;

            using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var safeName = $"[{tableName.Replace("]", "]]")}]";
            var query = $"IF OBJECT_ID('{safeName}', 'U') IS NOT NULL DROP TABLE {safeName}";
            using var cmd = new SqlCommand(query, connection);
            await cmd.ExecuteNonQueryAsync();
        }

        private static long ParseDataSize(object? sizeValue)
        {
            switch (sizeValue)
            {
                case null:
                    return 0L;
                case long longValue:
                    return longValue;
                case int intValue:
                    return intValue;
                case string stringValue when long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    var invariantText = System.Convert.ToString(sizeValue, CultureInfo.InvariantCulture);
                    return long.TryParse(invariantText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var converted)
                        ? converted
                        : 0L;
            }
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var metadonnee = await _context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metadonnee == null)
                return NotFound();

            var userId = GetCurrentUserId();
            var role = GetCurrentUserRole();
            if (role != RoleIds.Administrateur && metadonnee.IdUtilisateur != userId)
                return Forbid();

            if (metadonnee.TraitementEnCours == true)
            {
                TempData["Error"] = "Impossible de supprimer : traitement en cours.";
                return RedirectToAction("RechercheDonnees", "AccesDonnees");
            }

            await DropSqlTableIfExists(metadonnee.Donnees?.NomDeLaTable);
            if (metadonnee.DonneesEventLogs != null)
                await DropSqlTableIfExists(metadonnee.DonneesEventLogs.NomDeLaTable);
            if (metadonnee.DonneesContexteEnvironnemental != null)
                await DropSqlTableIfExists(metadonnee.DonneesContexteEnvironnemental.NomDeLaTable);

            var links = _context.Metadonnee_Appareil.Where(l => l.IdMetadonnee == id);
            _context.Metadonnee_Appareil.RemoveRange(links);
            if (metadonnee.DonneesEventLogs != null) _context.DonneesEventLogs.Remove(metadonnee.DonneesEventLogs);
            if (metadonnee.DonneesContexteEnvironnemental != null) _context.DonneesContexteEnvironnemental.Remove(metadonnee.DonneesContexteEnvironnemental);
            if (metadonnee.Donnees != null) _context.Donnees.Remove(metadonnee.Donnees);

            _context.Metadonnee.Remove(metadonnee);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Data deleted successfully.";
            return RedirectToAction("RechercheDonnees", "AccesDonnees");
        }
    }
}