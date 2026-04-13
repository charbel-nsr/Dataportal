using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Dataportal.Classes;
using Dataportal.Services;
using Dataportal.Helpers;
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
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using SystemTextJson = System.Text.Json.JsonSerializer;

namespace Dataportal.Controllers
{
    public class DonneesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ITabularFileImporter _fileImporter;
        private const string DataSizeTempDataKey = "DataSizeBytes";
        private const string UploadCacheFolderName = "dataportal-upload-cache";
        private const string UploadMetadataFileName = "metadata.json";
        private static readonly HashSet<string> AllowedUploadExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csv",
            ".xlsx",
            ".parquet",
            ".csv.zip"
        };
        private static readonly string[] KnownSchemas =
        {
            TableImportSchemas.Donnees,
            TableImportSchemas.DonneesEventLogs,
            TableImportSchemas.DonneesContexteEnvironnemental
        };

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
            HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 2);

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

        private IActionResult RedirectToCreationFallback(int? metadonneeId)
        {
            if (metadonneeId.HasValue)
            {
                return RedirectToAction("Details", new { id = metadonneeId.Value, creation = true });
            }

            HttpContext.Session.Remove(SessionKeys.CreationMetadonneeId);
            HttpContext.Session.Remove(SessionKeys.CreationNextStep);

            return RedirectToAction("Index", "Accueil");
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
                var extension = NormalizeUploadExtension(safeName);
                if (extension == null || !AllowedUploadExtensions.Contains(extension))
                {
                    throw new InvalidOperationException("Only CSV, XLSX, Parquet, or CSV.zip files are allowed.");
                }

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

        private static string? NormalizeUploadExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            if (fileName.EndsWith(".csv.zip", StringComparison.OrdinalIgnoreCase))
            {
                return ".csv.zip";
            }

            var extension = Path.GetExtension(fileName);
            return string.IsNullOrWhiteSpace(extension)
                ? null
                : extension.ToLowerInvariant();
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

        private static void PopulateIndexOptions(DonneesCreateStep2ViewModel model)
        {
            if (!model.IsTimeSeries || !model.ColumnTypesConfirmed || model.ColumnTypes == null || model.ColumnTypes.Count == 0)
            {
                model.IndexTimeColumnOptions = new List<SelectListItem>();
                model.IndexIdColumnOptions = new List<SelectListItem>();
                model.IndexIncludeColumnOptions = new List<SelectListItem>();
                return;
            }

            var (timeColumns, idColumns, includeColumns) = BuildIndexColumnOptions(model.ColumnTypes);
            model.IndexTimeColumnOptions = timeColumns;
            model.IndexIdColumnOptions = idColumns;
            model.IndexIncludeColumnOptions = includeColumns;
        }

        private static void PopulateIndexOptions(DonneesEventLogsCreateStep3ViewModel model)
        {
            if (!model.IsTimeSeries || !model.ColumnTypesConfirmed || model.ColumnTypes == null || model.ColumnTypes.Count == 0)
            {
                model.IndexTimeColumnOptions = new List<SelectListItem>();
                model.IndexIdColumnOptions = new List<SelectListItem>();
                model.IndexIncludeColumnOptions = new List<SelectListItem>();
                return;
            }

            var (timeColumns, idColumns, includeColumns) = BuildIndexColumnOptions(model.ColumnTypes);
            model.IndexTimeColumnOptions = timeColumns;
            model.IndexIdColumnOptions = idColumns;
            model.IndexIncludeColumnOptions = includeColumns;
        }

        private static void PopulateIndexOptions(DonneesContexteEnvironnementalCreateStep4ViewModel model)
        {
            if (!model.IsTimeSeries || !model.ColumnTypesConfirmed || model.ColumnTypes == null || model.ColumnTypes.Count == 0)
            {
                model.IndexTimeColumnOptions = new List<SelectListItem>();
                model.IndexIdColumnOptions = new List<SelectListItem>();
                model.IndexIncludeColumnOptions = new List<SelectListItem>();
                return;
            }

            var (timeColumns, idColumns, includeColumns) = BuildIndexColumnOptions(model.ColumnTypes);
            model.IndexTimeColumnOptions = timeColumns;
            model.IndexIdColumnOptions = idColumns;
            model.IndexIncludeColumnOptions = includeColumns;
        }

        private static (List<SelectListItem> TimeColumns, List<SelectListItem> IdColumns, List<SelectListItem> IncludeColumns)
            BuildIndexColumnOptions(IReadOnlyCollection<ColumnTypeSelectionViewModel> columnTypes)
        {
            if (columnTypes == null || columnTypes.Count == 0)
            {
                return (new List<SelectListItem>(), new List<SelectListItem>(), new List<SelectListItem>());
            }

            var eligibleColumns = columnTypes
                .Select(c =>
                {
                    var parsed = Enum.TryParse<TabularColumnType>(c.SelectedType, out var t) ? t : TabularColumnType.NVarChar;
                    return new { c.ColumnName, ColumnType = parsed };
                })
                .Where(c => c.ColumnType != TabularColumnType.NVarChar)
                .ToList();

            var timeColumns = eligibleColumns
                .Where(c => c.ColumnType == TabularColumnType.DateTime2)
                .Select(c => new SelectListItem { Value = c.ColumnName, Text = c.ColumnName })
                .ToList();

            var idColumns = eligibleColumns
                .Where(c => c.ColumnType != TabularColumnType.DateTime2)
                .Select(c => new SelectListItem { Value = c.ColumnName, Text = c.ColumnName })
                .ToList();

            var includeColumns = eligibleColumns
                .Select(c => new SelectListItem { Value = c.ColumnName, Text = c.ColumnName })
                .ToList();

            return (timeColumns, idColumns, includeColumns);
        }

        private string GetUploadSessionPath(string sessionId)
        {
            return Path.Combine(Path.GetTempPath(), UploadCacheFolderName, sessionId);
        }

        private enum ExistingLabelSource
        {
            Donnees,
            EventLogs,
            Contexte
        }

        private record PersistedFileMetadata(string StoredName, string OriginalFileName, string ContentType);

        private record UploadSession(string SessionId, List<IFormFile> Files, long TotalSize);

        private IQueryable<Metadonnee> BuildAccessibleMetadonneeQuery()
        {
            var baseQuery = _context.Metadonnee
                .Include(m => m.Utilisateur)
                .ThenInclude(u => u.Entreprise)
                .AsQueryable();

            var userId = TryGetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var userEntrepriseId = GetCurrentUserEntrepriseId();

            var isAuthenticated = userId.HasValue;
            var isAdmin = userRole == RoleIds.Administrateur;
            var isInternalRole = userRole == RoleIds.Utilisateur || userRole == RoleIds.Editeur;

            return baseQuery.Where(m =>
                m.IdVisibilite == VisibiliteIds.Public ||
                (m.IdVisibilite == VisibiliteIds.Prive && isAuthenticated) ||
                (
                    m.IdVisibilite == VisibiliteIds.Interne &&
                    isAuthenticated &&
                    (
                        isAdmin ||
                        (
                            isInternalRole &&
                            userEntrepriseId.HasValue &&
                            m.Utilisateur != null &&
                            m.Utilisateur.IdEntreprise == userEntrepriseId
                        )
                    )
                ) ||
                (
                    m.IdVisibilite == VisibiliteIds.Personnelle &&
                    (isAdmin || (isAuthenticated && m.IdUtilisateur == userId))
                ));
        }

        private async Task<List<ExistingLabelOption>> BuildExistingLabelOptionsAsync(ExistingLabelSource source)
        {
            var query = BuildAccessibleMetadonneeQuery();

            switch (source)
            {
                case ExistingLabelSource.Donnees:
                    return await query
                        .Include(m => m.Donnees)
                        .Where(m => m.Donnees != null)
                        .Select(m => new ExistingLabelOption
                        {
                            Value = m.Id.ToString(),
                            DisplayText = m.Donnees!.Libelle,
                            Libelle = m.Donnees.Libelle,
                            Code = m.Donnees.Code ?? string.Empty
                        })
                        .OrderBy(o => o.DisplayText)
                        .ToListAsync();
                case ExistingLabelSource.EventLogs:
                    return await query
                        .Include(m => m.DonneesEventLogs)
                        .Where(m => m.DonneesEventLogs != null)
                        .Select(m => new ExistingLabelOption
                        {
                            Value = m.Id.ToString(),
                            DisplayText = m.DonneesEventLogs!.Libelle,
                            Libelle = m.DonneesEventLogs.Libelle,
                            Code = m.DonneesEventLogs.Code ?? string.Empty
                        })
                        .OrderBy(o => o.DisplayText)
                        .ToListAsync();
                case ExistingLabelSource.Contexte:
                    return await query
                        .Include(m => m.DonneesContexteEnvironnemental)
                        .Where(m => m.DonneesContexteEnvironnemental != null)
                        .Select(m => new ExistingLabelOption
                        {
                            Value = m.Id.ToString(),
                            DisplayText = m.DonneesContexteEnvironnemental!.Libelle,
                            Libelle = m.DonneesContexteEnvironnemental.Libelle,
                            Code = m.DonneesContexteEnvironnemental.Code ?? string.Empty
                        })
                        .OrderBy(o => o.DisplayText)
                        .ToListAsync();
                default:
                    return new List<ExistingLabelOption>();
            }
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
                ExistingLabelSource.Donnees => _context.Donnees
                    .Where(d => d.Libelle.ToLower() == normalizedLibelle && d.Code != null)
                    .Select(d => d.Code!),
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
            var codesQuery = BuildCodesQueryForLabel(normalizedLibelle, source);
            var codes = await codesQuery.ToListAsync();

            var nextNumber = codes.Select(ExtractSuffixNumber).DefaultIfEmpty(0).Max() + 1;

            return $"{datePrefix}{nextNumber:D2}";
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public async Task<IActionResult> NextCode(string libelle, string? source)
        {
            if (string.IsNullOrWhiteSpace(libelle))
            {
                return BadRequest("Label is required to generate a code.");
            }

            var codeSource = source?.ToLower() switch
            {
                "eventlogs" => ExistingLabelSource.EventLogs,
                "contexte" => ExistingLabelSource.Contexte,
                _ => ExistingLabelSource.Donnees
            };

            var code = await GenerateNextCodeForLabelAsync(libelle, codeSource);

            return Json(new { code });
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public async Task<IActionResult> CreateStep2()
        {
            var (resumeId, nextStep) = GetCreationWizardState();

            if (resumeId.HasValue && nextStep.HasValue && nextStep.Value >= 3)
            {
                return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
            }

            if (!nextStep.HasValue || nextStep.Value < 2)
            {
                return RedirectToAction("Index", "Accueil");
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

            var step1Data = JsonConvert.DeserializeObject<MetadonneeCreateViewModel>(step1Json);

            // show the upload + Donnees form
            var qualiteData = BuildQualiteOptions();
            var vm = new DonneesCreateStep2ViewModel
            {
                StartTimestamp = DateTime.Now,
                EndTimestamp = DateTime.Now,
                QualiteOptions = qualiteData.Options,
                QualiteDescriptions = qualiteData.Descriptions,
                Code = await GenerateNextCodeForLabelAsync(null, ExistingLabelSource.Donnees),
                ExistingLabelOptions = await BuildExistingLabelOptionsAsync(ExistingLabelSource.Donnees),
                IsTimeSeries = step1Data?.SeriesTemporelles == true
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
            model.IsTimeSeries = step1Data?.SeriesTemporelles == true;

            void RepopulateQualiteSelections()
            {
                var qualiteDataLocal = BuildQualiteOptions();
                model.QualiteOptions = qualiteDataLocal.Options;
                model.QualiteDescriptions = qualiteDataLocal.Descriptions;
                PopulateIndexOptions(model);
            }

            model.ExistingLabelOptions = await BuildExistingLabelOptionsAsync(ExistingLabelSource.Donnees);
            var canConfigureIndex = User.IsInRole("administrator") || User.IsInRole("editor");

            if (!model.IsTimeSeries)
            {
                model.IndexEnabled = false;
                model.IndexTimeColumn = null;
                model.IndexIdColumn = null;
                model.IndexIncludeColumn = null;
            }
            else if (!canConfigureIndex)
            {
                model.IndexEnabled = false;
                model.IndexTimeColumn = null;
                model.IndexIdColumn = null;
                model.IndexIncludeColumn = null;
            }

            if (User.IsInRole("user") && step1Data.IdVisibilite != VisibiliteIds.Personnelle)
            {
                TempData.Remove("Step1Data");
                return Forbid();
            }

            if (model.IdExistingMetadonnee.HasValue)
            {
                var selected = model.ExistingLabelOptions
                    .FirstOrDefault(o => o.Value == model.IdExistingMetadonnee.Value.ToString());

                if (selected != null)
                {
                    model.Libelle = selected.Libelle;
                    model.Code = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.Donnees);
                }
                else
                {
                    ModelState.AddModelError(nameof(model.IdExistingMetadonnee), "The selected data is not available.");
                }
            }
            else
            {
                model.Code = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.Donnees);
            }
            ModelState.Remove(nameof(model.Code));
            TryValidateModel(model);

            if (!ModelState.IsValid)
            {
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            var normalizedLibelle = model.Libelle.Trim().ToLower();
            var normalizedCode = model.Code.Trim().ToLower();

            var expectedCode = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.Donnees);

            if (!string.Equals(model.Code.Trim(), expectedCode, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Code), "The code must use today's date and increment the version for this label.");
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

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
                uploadSession = await EnsureUploadSessionAsync(model.UploadedFiles, model.UploadSessionId, true);
                if (uploadSession == null)
                {
                    throw new InvalidOperationException("You must upload at least one data file (CSV, XLSX, Parquet, or CSV.zip).");
                }

                model.UploadSessionId = uploadSession.SessionId;
                model.PersistedFiles = BuildPersistedFileSummaries(uploadSession);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            if (uploadSession.TotalSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                return View(model);
            }

            var baseTableName = BuildBaseTableName(model.Libelle, model.Code);
            var tableName = BuildSchemaQualifiedName(TableImportSchemas.Donnees, baseTableName);
            var target = new TableImportTarget(TableImportSchemas.Donnees, baseTableName);

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
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                ModelState.Clear();
                return View(model);
            }

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

            if (importResult.Errors.Any() && !model.ProceedAfterImportErrors)
            {
                model.ImportErrors = importResult.Errors;
                model.ProceedAfterImportErrors = false;
                TempData.Keep("Step1Data");
                RepopulateQualiteSelections();
                ModelState.Clear();
                return View(model);
            }

            TempData[DataSizeTempDataKey] = uploadSession.TotalSize.ToString(CultureInfo.InvariantCulture);
            if (importResult.Errors.Any())
            {
                TempData["ImportWarnings"] = $"Imported with {importResult.Errors.Count} parsing issue(s). Invalid values were stored as NULL.";
            }

            var trimmedTimeColumn = model.IndexTimeColumn?.Trim();
            var trimmedIdColumn = model.IndexIdColumn?.Trim();
            var trimmedIncludeColumn = model.IndexIncludeColumn?.Trim();
            var indexAllowed = model.IsTimeSeries && model.IndexEnabled && canConfigureIndex;
            var indexType = indexAllowed
                ? (string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time only" : "time and ids")
                : null;
            var indexName = indexAllowed
                ? $"IX_{baseTableName}_{(string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time" : "time_id")}"
                : null;
            var indexStatus = indexAllowed ? "pending" : null;

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
                IdQualiteDonnees = model.IdQualiteDonnees!.Value,
                IndexEnabled = indexAllowed,
                IndexTimeColumn = indexAllowed ? trimmedTimeColumn : null,
                IndexIdColumn = indexAllowed ? trimmedIdColumn : null,
                IndexIncludeColumn = indexAllowed ? trimmedIncludeColumn : null,
                IndexType = indexAllowed ? indexType : null,
                IndexName = indexAllowed ? indexName : null,
                IndexStatus = indexAllowed ? indexStatus : null,
                IndexError = null
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
                TailleDesDonnees = FormatDataSize(uploadSession.TotalSize),
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

            DeleteUploadSession(model.UploadSessionId);
            model.UploadSessionId = null;

            return RedirectToAction("CreateStep3", new { id = metadonnee.Id });
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public async Task<IActionResult> CreateStep3(int id)
        {
            var (resumeId, nextStep) = GetCreationWizardState();

            if (!resumeId.HasValue || resumeId.Value != id || !nextStep.HasValue || nextStep.Value < 3)
            {
                return RedirectToCreationFallback(resumeId ?? id);
            }

            if (nextStep.Value > 3)
            {
                return RedirectToAction("Details", new { id = resumeId.Value, creation = true });
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
                QualiteDescriptions = qualiteData.Descriptions,
                Code = await GenerateNextCodeForLabelAsync(null, ExistingLabelSource.EventLogs),
                ExistingLabelOptions = await BuildExistingLabelOptionsAsync(ExistingLabelSource.EventLogs),
                IsTimeSeries = metadonnee.SeriesTemporelles
            };
            return View(vm);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep3(DonneesEventLogsCreateStep3ViewModel model)
        {
            var metadonnee = await _context.Metadonnee.FindAsync(model.IdMetadonnee);
            if (metadonnee == null)
            {
                TempData["Error"] = "Metadata not found.";
                return RedirectToAction("CreateStep1");
            }

            model.IsTimeSeries = metadonnee.SeriesTemporelles;
            var canConfigureIndex = User.IsInRole("administrator") || User.IsInRole("editor");

            void RepopulateQualiteSelections()
            {
                var qualiteDataLocal = BuildQualiteOptions();
                model.QualiteOptions = qualiteDataLocal.Options;
                model.QualiteDescriptions = qualiteDataLocal.Descriptions;
                PopulateIndexOptions(model);
            }

            model.ExistingLabelOptions = await BuildExistingLabelOptionsAsync(ExistingLabelSource.EventLogs);

            if (!model.IsTimeSeries || !canConfigureIndex)
            {
                model.IndexEnabled = false;
                model.IndexTimeColumn = null;
                model.IndexIdColumn = null;
                model.IndexIncludeColumn = null;
            }

            if (model.IdExistingMetadonnee.HasValue)
            {
                var selected = model.ExistingLabelOptions
                    .FirstOrDefault(o => o.Value == model.IdExistingMetadonnee.Value.ToString());

                if (selected != null)
                {
                    model.Libelle = selected.Libelle;
                    model.Code = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.EventLogs);
                }
                else
                {
                    ModelState.AddModelError(nameof(model.IdExistingMetadonnee), "The selected event logs are not available.");
                }
            }
            else
            {
                model.Code = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.EventLogs);
            }
            ModelState.Remove(nameof(model.Code));
            TryValidateModel(model);

            if (!ModelState.IsValid)
            {
                RepopulateQualiteSelections();
                return View(model);
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            // Handle skip: if no file uploaded, user wants to skip
            if ((model.UploadedFiles == null || !model.UploadedFiles.Any()) && string.IsNullOrWhiteSpace(model.UploadSessionId))
            {
                HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 4);
                return RedirectToAction("CreateStep4", new { id = model.IdMetadonnee });
            }

            var normalizedLibelle = model.Libelle.Trim().ToLower();
            var normalizedCode = model.Code.Trim().ToLower();

            var expectedCode = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.EventLogs);

            if (!string.Equals(model.Code.Trim(), expectedCode, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Code), "The code must use today's date and increment the version for this label.");
                RepopulateQualiteSelections();
                return View(model);
            }

            var duplicate = await _context.DonneesEventLogs.FirstOrDefaultAsync(e =>
                            e.Libelle.ToLower() == normalizedLibelle &&
                            e.Code.ToLower() == normalizedCode);

            if (duplicate != null)
            {
                ModelState.AddModelError("Libelle", "This label/code combination already exists.");
                ModelState.AddModelError("Code", "This label/code combination already exists.");

                RepopulateQualiteSelections();
                return View(model);
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
                    HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 4);
                    return RedirectToAction("CreateStep4", new { id = model.IdMetadonnee });
                }

                model.UploadSessionId = uploadSession.SessionId;
                model.PersistedFiles = BuildPersistedFileSummaries(uploadSession);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                RepopulateQualiteSelections();
                return View(model);
            }

            if (uploadSession.TotalSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                RepopulateQualiteSelections();
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
                RepopulateQualiteSelections();
                ModelState.Clear();
                return View(model);
            }

            // Update data size with uploaded files
            long existingDataSize = 0;
            var sizeValue = TempData.Peek(DataSizeTempDataKey);
            if (sizeValue != null)
            {
                existingDataSize = ParseDataSize(sizeValue);
            }
            long stepSize = uploadSession.TotalSize;
            long totalDataSize = existingDataSize + stepSize;

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

            if (importResult.Errors.Any() && !model.ProceedAfterImportErrors)
            {
                model.ImportErrors = importResult.Errors;
                model.ProceedAfterImportErrors = false;
                RepopulateQualiteSelections();
                ModelState.Clear();
                return View(model);
            }

            TempData[DataSizeTempDataKey] = totalDataSize.ToString(CultureInfo.InvariantCulture);
            if (importResult.Errors.Any())
            {
                TempData["ImportWarnings"] = $"Imported with {importResult.Errors.Count} parsing issue(s). Invalid values were stored as NULL.";
            }

            var trimmedTimeColumn = model.IndexTimeColumn?.Trim();
            var trimmedIdColumn = model.IndexIdColumn?.Trim();
            var trimmedIncludeColumn = model.IndexIncludeColumn?.Trim();
            var indexAllowed = model.IsTimeSeries && model.IndexEnabled && canConfigureIndex;
            var indexType = indexAllowed
                ? (string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time only" : "time and ids")
                : null;
            var indexName = indexAllowed
                ? $"IX_{baseTableName}_{(string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time" : "time_id")}"
                : null;
            var indexStatus = indexAllowed ? "pending" : null;

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
                IdQualiteDonnees = model.IdQualiteDonnees!.Value,
                IndexEnabled = indexAllowed,
                IndexTimeColumn = indexAllowed ? trimmedTimeColumn : null,
                IndexIdColumn = indexAllowed ? trimmedIdColumn : null,
                IndexIncludeColumn = indexAllowed ? trimmedIncludeColumn : null,
                IndexType = indexAllowed ? indexType : null,
                IndexName = indexAllowed ? indexName : null,
                IndexStatus = indexAllowed ? indexStatus : null,
                IndexError = null
            };

            _context.DonneesEventLogs.Add(eventLogs);
            await _context.SaveChangesAsync();

            // Link to Metadonnee and update data size
            metadonnee.IdDonneesEventLogs = eventLogs.Id;
            metadonnee.TailleDesDonnees = FormatDataSize(totalDataSize);
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 4);

            DeleteUploadSession(model.UploadSessionId);
            model.UploadSessionId = null;

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
        public async Task<IActionResult> CreateStep4(int id)
        {
            var (resumeId, nextStep) = GetCreationWizardState();

            if (!resumeId.HasValue || resumeId.Value != id)
            {
                return RedirectToCreationFallback(id);
            }

            if (!nextStep.HasValue || nextStep.Value < 4)
            {
                return RedirectToAction("CreateStep3", new { id = resumeId.Value });
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
                QualiteDescriptions = qualiteData.Descriptions,
                Code = await GenerateNextCodeForLabelAsync(null, ExistingLabelSource.Contexte),
                ExistingLabelOptions = await BuildExistingLabelOptionsAsync(ExistingLabelSource.Contexte),
                IsTimeSeries = metadonnee.SeriesTemporelles
            };
            return View(vm);
        }

        // POST: /Donnees/CreateStep4
        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep4(DonneesContexteEnvironnementalCreateStep4ViewModel model)
        {
            var metadonnee = await _context.Metadonnee.FindAsync(model.IdMetadonnee);
            if (metadonnee == null)
            {
                TempData["Error"] = "Metadata not found.";
                return RedirectToAction("CreateStep1");
            }

            model.IsTimeSeries = metadonnee.SeriesTemporelles;
            var canConfigureIndex = User.IsInRole("administrator") || User.IsInRole("editor");

            void RepopulateQualiteSelections()
            {
                var qualiteDataLocal = BuildQualiteOptions();
                model.QualiteOptions = qualiteDataLocal.Options;
                model.QualiteDescriptions = qualiteDataLocal.Descriptions;
                PopulateIndexOptions(model);
            }

            model.ExistingLabelOptions = await BuildExistingLabelOptionsAsync(ExistingLabelSource.Contexte);

            if (!model.IsTimeSeries || !canConfigureIndex)
            {
                model.IndexEnabled = false;
                model.IndexTimeColumn = null;
                model.IndexIdColumn = null;
                model.IndexIncludeColumn = null;
            }

            if (model.IdExistingMetadonnee.HasValue)
            {
                var selected = model.ExistingLabelOptions
                    .FirstOrDefault(o => o.Value == model.IdExistingMetadonnee.Value.ToString());

                if (selected != null)
                {
                    model.Libelle = selected.Libelle;
                    model.Code = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.Contexte);
                }
                else
                {
                    ModelState.AddModelError(nameof(model.IdExistingMetadonnee), "The selected environmental context is not available.");
                }
            }
            else
            {
                model.Code = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.Contexte);
            }

            ModelState.Remove(nameof(model.Code));
            TryValidateModel(model);

            if (!ModelState.IsValid)
            {
                RepopulateQualiteSelections();
                return View(model);
            }

            if (User.IsInRole("user") && metadonnee.IdVisibilite != VisibiliteIds.Personnelle)
            {
                return Forbid();
            }

            if ((model.UploadedFiles == null || !model.UploadedFiles.Any()) && string.IsNullOrWhiteSpace(model.UploadSessionId))
            {
                // User skipped uploading: go straight to next step
                HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 5);
                TempData.Remove(DataSizeTempDataKey);
                return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
            }

            var normalizedLibelle = model.Libelle.Trim().ToLower();
            var normalizedCode = model.Code.Trim().ToLower();

            var expectedCode = await GenerateNextCodeForLabelAsync(model.Libelle, ExistingLabelSource.Contexte);

            if (!string.Equals(model.Code.Trim(), expectedCode, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(model.Code), "The code must use today's date and increment the version for this label.");
                RepopulateQualiteSelections();
                return View(model);
            }

            var duplicate = await _context.DonneesContexteEnvironnemental.FirstOrDefaultAsync(c =>
                            c.Libelle.ToLower() == normalizedLibelle &&
                            c.Code.ToLower() == normalizedCode);

            if (duplicate != null)
            {
                ModelState.AddModelError("Libelle", "This label/code combination already exists.");
                ModelState.AddModelError("Code", "This label/code combination already exists.");

                RepopulateQualiteSelections();
                return View(model);
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
                    HttpContext.Session.SetInt32(SessionKeys.CreationNextStep, 5);
                    TempData.Remove(DataSizeTempDataKey);
                    return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
                }

                model.UploadSessionId = uploadSession.SessionId;
                model.PersistedFiles = BuildPersistedFileSummaries(uploadSession);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), ex.Message);
                RepopulateQualiteSelections();
                return View(model);
            }

            if (uploadSession.TotalSize > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFiles), $"The total size of the uploaded files exceeds the {UploadSizeLimits.StepUploadLimitDisplay} limit for this step.");
                RepopulateQualiteSelections();
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
                RepopulateQualiteSelections();
                ModelState.Clear();
                return View(model);
            }

            // Update data size with uploaded files
            long existingDataSize = 0;
            var sizeValue = TempData.Peek(DataSizeTempDataKey);
            if (sizeValue != null)
            {
                existingDataSize = ParseDataSize(sizeValue);
            }
            long stepSize = uploadSession.TotalSize;
            long totalDataSize = existingDataSize + stepSize;

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

            if (importResult.Errors.Any() && !model.ProceedAfterImportErrors)
            {
                model.ImportErrors = importResult.Errors;
                model.ProceedAfterImportErrors = false;
                RepopulateQualiteSelections();
                ModelState.Clear();
                return View(model);
            }

            TempData[DataSizeTempDataKey] = totalDataSize.ToString(CultureInfo.InvariantCulture);
            if (importResult.Errors.Any())
            {
                TempData["ImportWarnings"] = $"Imported with {importResult.Errors.Count} parsing issue(s). Invalid values were stored as NULL.";
            }

            var trimmedTimeColumn = model.IndexTimeColumn?.Trim();
            var trimmedIdColumn = model.IndexIdColumn?.Trim();
            var trimmedIncludeColumn = model.IndexIncludeColumn?.Trim();
            var indexAllowed = model.IsTimeSeries && model.IndexEnabled && canConfigureIndex;
            var indexType = indexAllowed
                ? (string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time only" : "time and ids")
                : null;
            var indexName = indexAllowed
                ? $"IX_{baseTableName}_{(string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time" : "time_id")}"
                : null;
            var indexStatus = indexAllowed ? "pending" : null;

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
                IdQualiteDonnees = model.IdQualiteDonnees!.Value,
                IndexEnabled = indexAllowed,
                IndexTimeColumn = indexAllowed ? trimmedTimeColumn : null,
                IndexIdColumn = indexAllowed ? trimmedIdColumn : null,
                IndexIncludeColumn = indexAllowed ? trimmedIncludeColumn : null,
                IndexType = indexAllowed ? indexType : null,
                IndexName = indexAllowed ? indexName : null,
                IndexStatus = indexAllowed ? indexStatus : null,
                IndexError = null
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

            DeleteUploadSession(model.UploadSessionId);
            model.UploadSessionId = null;

            // Proceed to next step
            return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public async Task<IActionResult> CreateIndexation(int id, string? returnUrl)
        {
            var metadonnee = await GetMetadonneeWithTablesAsync(id);

            if (metadonnee == null)
            {
                return NotFound();
            }

            var isAdmin = User.IsInRole("administrator");
            var userId = TryGetCurrentUserId();
            if (!isAdmin && userId.HasValue && metadonnee.IdUtilisateur != userId.Value)
            {
                return Forbid();
            }

            if (metadonnee.TraitementEnCours == true)
            {
                TempData["Error"] = "Impossible de demander une indexation : traitement en cours.";
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Details", new { id = metadonnee.Id });
            }

            var viewModel = await BuildIndexationPageViewModelAsync(metadonnee, returnUrl);
            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateIndexation(IndexationRequestViewModel model)
        {
            if (!TryGetIndexationSchema(model.RecordType, out var schema))
            {
                return NotFound();
            }

            var isAdmin = User.IsInRole("administrator");
            var userId = TryGetCurrentUserId();

            var (metadonnee, record) = await GetIndexationRecordAsync(model.RecordType, model.RecordId);
            if (record == null)
            {
                return NotFound();
            }

            if (metadonnee == null)
            {
                return NotFound();
            }

            if (!isAdmin && userId.HasValue && metadonnee.IdUtilisateur != userId.Value)
            {
                return Forbid();
            }

            if (metadonnee.TraitementEnCours == true)
            {
                TempData["Error"] = "Impossible de demander une indexation : traitement en cours.";
                if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Details", new { id = metadonnee.Id });
            }

            if (!TryBuildTableImportTarget(record.TableName, schema, out var target) || target == null)
            {
                ModelState.AddModelError(string.Empty, "Unable to locate the source table for indexing.");
            }

            if (metadonnee?.SeriesTemporelles != true)
            {
                ModelState.AddModelError(string.Empty, "Indexation is only available for time-series datasets.");
            }

            if (!model.IndexEnabled)
            {
                ModelState.AddModelError(nameof(model.IndexEnabled), "Enable indexing to submit this request.");
            }

            var columnTypes = target != null
                ? await GetColumnTypeSelectionsAsync(target)
                : new List<ColumnTypeSelectionViewModel>();
            var (timeColumns, idColumns, includeColumns) = BuildIndexColumnOptions(columnTypes);

            model.IndexTimeColumnOptions = timeColumns;
            model.IndexIdColumnOptions = idColumns;
            model.IndexIncludeColumnOptions = includeColumns;
            model.IsTimeSeries = metadonnee?.SeriesTemporelles == true;
            model.TableName = record.TableName;
            model.DatasetName = metadonnee?.Nom ?? record.DisplayName;
            model.MetadonneeId = metadonnee?.Id ?? 0;

            if (!ModelState.IsValid)
            {
                if (metadonnee == null)
                {
                    return NotFound();
                }

                var pageMetadonnee = await GetMetadonneeWithTablesAsync(metadonnee.Id);
                if (pageMetadonnee == null)
                {
                    return NotFound();
                }

                var pageModel = await BuildIndexationPageViewModelAsync(pageMetadonnee, model.ReturnUrl, model);
                return View(pageModel);
            }

            if (record.IndexEnabled)
            {
                TempData["Error"] = "Indexation is already enabled for this dataset.";
                return RedirectToAction("Details", new { id = metadonnee?.Id ?? 0 });
            }

            var trimmedTimeColumn = model.IndexTimeColumn?.Trim();
            var trimmedIdColumn = model.IndexIdColumn?.Trim();
            var trimmedIncludeColumn = model.IndexIncludeColumn?.Trim();

            var baseTableName = GetBaseTableName(record.TableName);
            var indexType = string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time only" : "time and ids";
            var indexName = $"IX_{baseTableName}_{(string.IsNullOrWhiteSpace(trimmedIdColumn) ? "time" : "time_id")}";

            record.ApplyIndexation(
                model.IndexEnabled,
                trimmedTimeColumn,
                trimmedIdColumn,
                trimmedIncludeColumn,
                indexType,
                indexName);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Indexation has been scheduled. The index will be built overnight or from the Indexation settings.";

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("CreateIndexation", new { id = metadonnee?.Id ?? 0 });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Details(int id, bool? creation, bool? modification, string? returnUrl)
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

            var modificationResumeId = HttpContext.Session.GetInt32(SessionKeys.ModificationMetadonneeId);
            if (modification == true && modificationResumeId.HasValue && modificationResumeId.Value == metadonnee.Id)
            {
                HttpContext.Session.Remove(SessionKeys.ModificationMetadonneeId);
                HttpContext.Session.Remove(SessionKeys.ModificationNextStep);
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

            if (creation == true || modification == true)
            {
                ViewData["CurrentStep"] = 5;
            }

            //Load preview data
            var donneesPreview = TryBuildTableImportTarget(donnees?.NomDeLaTable, TableImportSchemas.Donnees, out var donneesTarget)
                ? await GetTablePreviewRows(donneesTarget!)
                : null;
            var eventLogsPreview = TryBuildTableImportTarget(eventLogs?.NomDeLaTable, TableImportSchemas.DonneesEventLogs, out var eventLogsTarget)
                ? await GetTablePreviewRows(eventLogsTarget!)
                : null;
            var contextePreview = TryBuildTableImportTarget(contexte?.NomDeLaTable, TableImportSchemas.DonneesContexteEnvironnemental, out var contexteTarget)
                ? await GetTablePreviewRows(contexteTarget!)
                : null;

            var useCreationFallback = creation == true || modification == true;
            var fallbackReturnUrl = useCreationFallback
                ? Url.Action("RechercheDonnees", "AccesDonnees")
                : Url.Action("Index", "Accueil");
            var resolvedReturnUrl = useCreationFallback
                ? fallbackReturnUrl
                : ResolveReturnUrl(returnUrl, fallbackReturnUrl);

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
                ContextePreviewRows = contextePreview,
                ReturnUrl = resolvedReturnUrl
            };

            return View(vm);
        }

        private static readonly Regex TableLabelWhitespaceRegex = new Regex("\\s+", RegexOptions.Compiled);
        private static readonly Regex TableLabelInvalidCharsRegex = new Regex("[^A-Za-z0-9-]", RegexOptions.Compiled);
        private static readonly Regex TableLabelDashCollapseRegex = new Regex("-{2,}", RegexOptions.Compiled);

        private static string BuildBaseTableName(string libelle, string code)
        {
            var normalizedLabel = NormalizeTableLabel(libelle);
            if (string.IsNullOrWhiteSpace(normalizedLabel))
            {
                normalizedLabel = "dataset";
            }

            return $"{normalizedLabel}-{code}";
        }

        private static string NormalizeTableLabel(string libelle)
        {
            if (string.IsNullOrWhiteSpace(libelle))
            {
                return string.Empty;
            }

            var trimmed = libelle.Trim();
            var dashed = TableLabelWhitespaceRegex.Replace(trimmed, "-");
            var sanitized = TableLabelInvalidCharsRegex.Replace(dashed, string.Empty);
            var collapsed = TableLabelDashCollapseRegex.Replace(sanitized, "-");
            return collapsed.Trim('-');
        }

        private static string BuildSchemaQualifiedName(string schema, string tableName)
        {
            return $"{schema}.{tableName}";
        }

        private static bool TryBuildTableImportTarget(string? storedName, string fallbackSchema, out TableImportTarget? target)
        {
            target = null;

            if (string.IsNullOrWhiteSpace(storedName))
            {
                return false;
            }

            var segments = storedName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 2 && IsKnownSchema(segments[0]))
            {
                target = new TableImportTarget(segments[0], segments[1]);
                return true;
            }

            target = new TableImportTarget(fallbackSchema, storedName.Trim());
            return true;
        }

        private static bool IsKnownSchema(string? schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
            {
                return false;
            }

            return KnownSchemas.Any(s => s.Equals(schema, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryGetIndexationSchema(string? type, out string schema)
        {
            schema = type switch
            {
                "donnees" => TableImportSchemas.Donnees,
                "eventlogs" => TableImportSchemas.DonneesEventLogs,
                "contexte" => TableImportSchemas.DonneesContexteEnvironnemental,
                _ => string.Empty
            };

            return !string.IsNullOrWhiteSpace(schema);
        }

        private async Task<List<ColumnTypeSelectionViewModel>> GetColumnTypeSelectionsAsync(TableImportTarget target)
        {
            var columns = new List<ColumnTypeSelectionViewModel>();
            const string query = @"
                SELECT COLUMN_NAME, DATA_TYPE
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table
                ORDER BY ORDINAL_POSITION";

            await using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();
            await using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@schema", target.Schema);
            command.Parameters.AddWithValue("@table", target.TableName);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var dataType = reader.GetString(1);
                var tabularType = MapSqlTypeToTabular(dataType);

                columns.Add(new ColumnTypeSelectionViewModel
                {
                    ColumnName = name,
                    SelectedType = tabularType.ToString(),
                    InferredType = dataType
                });
            }

            return columns;
        }

        private static TabularColumnType MapSqlTypeToTabular(string? dataType)
        {
            if (string.IsNullOrWhiteSpace(dataType))
            {
                return TabularColumnType.NVarChar;
            }

            return dataType.Trim().ToLowerInvariant() switch
            {
                "bit" => TabularColumnType.Bit,
                "int" => TabularColumnType.Int,
                "bigint" => TabularColumnType.BigInt,
                "decimal" => TabularColumnType.Decimal,
                "numeric" => TabularColumnType.Decimal,
                "money" => TabularColumnType.Decimal,
                "smallmoney" => TabularColumnType.Decimal,
                "float" => TabularColumnType.Float,
                "real" => TabularColumnType.Float,
                "date" => TabularColumnType.DateTime2,
                "datetime" => TabularColumnType.DateTime2,
                "datetime2" => TabularColumnType.DateTime2,
                "smalldatetime" => TabularColumnType.DateTime2,
                "datetimeoffset" => TabularColumnType.DateTime2,
                _ => TabularColumnType.NVarChar
            };
        }

        private static string GetBaseTableName(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                return "dataset";
            }

            var segments = tableName.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 2 ? segments[1] : tableName.Trim();
        }

        private static (string TabId, string TabTitle) GetIndexationTabInfo(string recordType)
        {
            return recordType switch
            {
                "donnees" => ("data", "Data"),
                "eventlogs" => ("eventLogs", "Event logs"),
                "contexte" => ("environmental", "Environmental context"),
                _ => ("data", "Data")
            };
        }

        private async Task<Metadonnee?> GetMetadonneeWithTablesAsync(int id)
        {
            return await _context.Metadonnee
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        private async Task<IndexationPageViewModel> BuildIndexationPageViewModelAsync(
            Metadonnee metadonnee,
            string? returnUrl,
            IndexationRequestViewModel? activeRequest = null)
        {
            var requests = new List<IndexationRequestViewModel>();
            if (metadonnee.Donnees != null)
            {
                requests.Add(await BuildIndexationRequestAsync(
                    metadonnee,
                    "donnees",
                    metadonnee.Donnees.Id,
                    metadonnee.Donnees.NomDeLaTable,
                    metadonnee.Donnees.IndexEnabled,
                    "data",
                    "Data",
                    returnUrl));
            }

            if (metadonnee.DonneesEventLogs != null)
            {
                requests.Add(await BuildIndexationRequestAsync(
                    metadonnee,
                    "eventlogs",
                    metadonnee.DonneesEventLogs.Id,
                    metadonnee.DonneesEventLogs.NomDeLaTable,
                    metadonnee.DonneesEventLogs.IndexEnabled,
                    "eventLogs",
                    "Event logs",
                    returnUrl));
            }

            if (metadonnee.DonneesContexteEnvironnemental != null)
            {
                requests.Add(await BuildIndexationRequestAsync(
                    metadonnee,
                    "contexte",
                    metadonnee.DonneesContexteEnvironnemental.Id,
                    metadonnee.DonneesContexteEnvironnemental.NomDeLaTable,
                    metadonnee.DonneesContexteEnvironnemental.IndexEnabled,
                    "environmental",
                    "Environmental context",
                    returnUrl));
            }

            if (activeRequest != null)
            {
                var matching = requests.FirstOrDefault(r =>
                    r.RecordType == activeRequest.RecordType &&
                    r.RecordId == activeRequest.RecordId);
                if (matching != null)
                {
                    matching.IndexEnabled = activeRequest.IndexEnabled;
                    matching.IndexTimeColumn = activeRequest.IndexTimeColumn;
                    matching.IndexIdColumn = activeRequest.IndexIdColumn;
                    matching.IndexIncludeColumn = activeRequest.IndexIncludeColumn;
                }
            }

            var activeTabId = requests.FirstOrDefault()?.TabId ?? string.Empty;
            if (activeRequest != null)
            {
                activeTabId = GetIndexationTabInfo(activeRequest.RecordType).TabId;
            }

            return new IndexationPageViewModel
            {
                MetadonneeId = metadonnee.Id,
                DatasetName = metadonnee.Nom,
                ReturnUrl = returnUrl,
                Requests = requests,
                ActiveTabId = activeTabId
            };
        }

        private async Task<IndexationRequestViewModel> BuildIndexationRequestAsync(
            Metadonnee metadonnee,
            string recordType,
            int recordId,
            string tableName,
            bool isIndexed,
            string tabId,
            string tabTitle,
            string? returnUrl)
        {
            var request = new IndexationRequestViewModel
            {
                TabId = tabId,
                TabTitle = tabTitle,
                RecordId = recordId,
                RecordType = recordType,
                MetadonneeId = metadonnee.Id,
                DatasetName = metadonnee.Nom,
                TableName = tableName,
                IsTimeSeries = metadonnee.SeriesTemporelles,
                IsIndexed = isIndexed,
                IndexEnabled = true,
                ReturnUrl = returnUrl
            };

            if (request.IsTimeSeries && !request.IsIndexed && TryGetIndexationSchema(recordType, out var schema))
            {
                if (TryBuildTableImportTarget(tableName, schema, out var target) && target != null)
                {
                    var columnTypes = await GetColumnTypeSelectionsAsync(target);
                    var (timeColumns, idColumns, includeColumns) = BuildIndexColumnOptions(columnTypes);
                    request.IndexTimeColumnOptions = timeColumns;
                    request.IndexIdColumnOptions = idColumns;
                    request.IndexIncludeColumnOptions = includeColumns;
                }
            }

            return request;
        }

        private async Task<(Metadonnee? Metadonnee, IndexationRecord? Record)> GetIndexationRecordAsync(string type, int id)
        {
            switch (type)
            {
                case "donnees":
                    var donnees = await _context.Donnees
                        .Include(d => d.Metadonnee)
                        .FirstOrDefaultAsync(d => d.Id == id);
                    if (donnees == null)
                    {
                        return (null, null);
                    }

                    return (donnees.Metadonnee, new IndexationRecord(
                        donnees.Id,
                        donnees.NomDeLaTable,
                        donnees.Libelle,
                        donnees.IndexEnabled,
                        (enabled, time, idColumn, includeColumn, indexType, indexName) =>
                        {
                            donnees.IndexEnabled = enabled;
                            donnees.IndexTimeColumn = time;
                            donnees.IndexIdColumn = idColumn;
                            donnees.IndexIncludeColumn = includeColumn;
                            donnees.IndexType = indexType;
                            donnees.IndexName = indexName;
                            donnees.IndexStatus = enabled ? "pending" : null;
                            donnees.IndexError = null;
                        }));
                case "eventlogs":
                    var eventLogs = await _context.DonneesEventLogs
                        .Include(d => d.Metadonnee)
                        .FirstOrDefaultAsync(d => d.Id == id);
                    if (eventLogs == null)
                    {
                        return (null, null);
                    }

                    return (eventLogs.Metadonnee, new IndexationRecord(
                        eventLogs.Id,
                        eventLogs.NomDeLaTable,
                        eventLogs.Libelle,
                        eventLogs.IndexEnabled,
                        (enabled, time, idColumn, includeColumn, indexType, indexName) =>
                        {
                            eventLogs.IndexEnabled = enabled;
                            eventLogs.IndexTimeColumn = time;
                            eventLogs.IndexIdColumn = idColumn;
                            eventLogs.IndexIncludeColumn = includeColumn;
                            eventLogs.IndexType = indexType;
                            eventLogs.IndexName = indexName;
                            eventLogs.IndexStatus = enabled ? "pending" : null;
                            eventLogs.IndexError = null;
                        }));
                case "contexte":
                    var contexte = await _context.DonneesContexteEnvironnemental
                        .Include(d => d.Metadonnee)
                        .FirstOrDefaultAsync(d => d.Id == id);
                    if (contexte == null)
                    {
                        return (null, null);
                    }

                    return (contexte.Metadonnee, new IndexationRecord(
                        contexte.Id,
                        contexte.NomDeLaTable,
                        contexte.Libelle,
                        contexte.IndexEnabled,
                        (enabled, time, idColumn, includeColumn, indexType, indexName) =>
                        {
                            contexte.IndexEnabled = enabled;
                            contexte.IndexTimeColumn = time;
                            contexte.IndexIdColumn = idColumn;
                            contexte.IndexIncludeColumn = includeColumn;
                            contexte.IndexType = indexType;
                            contexte.IndexName = indexName;
                            contexte.IndexStatus = enabled ? "pending" : null;
                            contexte.IndexError = null;
                        }));
                default:
                    return (null, null);
            }
        }

        private sealed record IndexationRecord(
            int RecordId,
            string TableName,
            string DisplayName,
            bool IndexEnabled,
            Action<bool, string?, string?, string?, string?, string?> ApplyIndexation);

        private string? ResolveReturnUrl(string? requestedReturnUrl, string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(requestedReturnUrl) && Url.IsLocalUrl(requestedReturnUrl))
            {
                return requestedReturnUrl;
            }

            var referer = Request.GetTypedHeaders().Referer?.ToString();
            if (!string.IsNullOrWhiteSpace(referer))
            {
                var currentHost = $"{Request.Scheme}://{Request.Host}";
                if (referer.StartsWith(currentHost, StringComparison.OrdinalIgnoreCase))
                {
                    var localPath = referer.Substring(currentHost.Length);
                    if (Url.IsLocalUrl(localPath))
                    {
                        return localPath;
                    }
                }
            }

            return fallback;
        }

        private async Task<List<Dictionary<string, object>>> GetTablePreviewRows(TableImportTarget target)
        {
            var results = new List<Dictionary<string, object>>();

            using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var query = $"SELECT TOP 10 * FROM {target.QualifiedNameWithDatabase}";
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


        private async Task DropSqlTableIfExists(TableImportTarget target)
        {
            using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var query = $"IF OBJECT_ID('{target.ObjectIdLiteral}', 'U') IS NOT NULL DROP TABLE {target.QualifiedNameWithDatabase}";
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

            if (TryBuildTableImportTarget(metadonnee.Donnees?.NomDeLaTable, TableImportSchemas.Donnees, out var donneesTarget))
                await DropSqlTableIfExists(donneesTarget!);
            if (TryBuildTableImportTarget(metadonnee.DonneesEventLogs?.NomDeLaTable, TableImportSchemas.DonneesEventLogs, out var donneesEventLogsTarget))
                await DropSqlTableIfExists(donneesEventLogsTarget!);
            if (TryBuildTableImportTarget(metadonnee.DonneesContexteEnvironnemental?.NomDeLaTable, TableImportSchemas.DonneesContexteEnvironnemental, out var donneesContexteTarget))
                await DropSqlTableIfExists(donneesContexteTarget!);

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
