using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.AspNetCore.StaticFiles;

namespace Dataportal.Controllers
{
    [AllowAnonymous]
    public class FilesController : Controller
    {
        private const string StoredFilesFolderName = "stored-files";
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".csv", ".xlsx", ".xls", ".parquet", ".zip", ".txt", ".pdf", ".json", ".png", ".jpg", ".jpeg", ".wav", ".mat"
        };
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FilesController> _logger;

        public FilesController(ApplicationDbContext context, IWebHostEnvironment environment, ILogger<FilesController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index(
            string? search,
            double? minFileSizeMb,
            double? maxFileSizeMb,
            int? idCreateur,
            int? idVisibilite,
            int? idLicence,
            int? idEntreprise,
            bool? autoriserLeTelechargement)
        {
            var baseQuery = _context.FichierStocke
                .Include(f => f.Visibilite)
                .Include(f => f.Utilisateur)
                    .ThenInclude(u => u.Entreprise)
                .Include(f => f.Licence)
                .Include(f => f.TypeEnergieRenouvelable)
                .AsQueryable();

            baseQuery = ApplyVisibilityFilter(baseQuery);

            var availableCreators = baseQuery
                .Where(f => f.Utilisateur != null)
                .Select(f => new { f.IdUtilisateur, f.Utilisateur.Prenom, f.Utilisateur.Nom })
                .Distinct()
                .ToList()
                .Select(x => new LookupItem
                {
                    Id = x.IdUtilisateur,
                    Label = $"{x.Prenom} {x.Nom}".Trim()
                })
                .OrderBy(x => x.Label)
                .ToList();

            var availableVisibilites = baseQuery
                .Select(f => new { f.IdVisibilite, f.Visibilite!.Libelle })
                .Distinct()
                .ToList()
                .Select(x => new LookupItem
                {
                    Id = x.IdVisibilite,
                    Label = x.Libelle
                })
                .OrderBy(x => x.Label)
                .ToList();

            var availableEntreprises = baseQuery
                .Where(f => f.Utilisateur != null && f.Utilisateur.Entreprise != null)
                .Select(f => new { f.Utilisateur.Entreprise!.Id, f.Utilisateur.Entreprise.Nom })
                .Distinct()
                .ToList()
                .Select(x => new LookupItem
                {
                    Id = x.Id,
                    Label = x.Nom
                })
                .OrderBy(x => x.Label)
                .ToList();

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(f =>
                    f.Nom.Contains(search) ||
                    (f.Description != null && f.Description.Contains(search)) ||
                    f.NomFichierOriginal.Contains(search));
            }

            if (idCreateur.HasValue) query = query.Where(f => f.IdUtilisateur == idCreateur);
            if (idVisibilite.HasValue) query = query.Where(f => f.IdVisibilite == idVisibilite);
            if (idLicence.HasValue) query = query.Where(f => f.IdLicence == idLicence);
            if (idEntreprise.HasValue) query = query.Where(f => f.Utilisateur != null && f.Utilisateur.IdEntreprise == idEntreprise);
            if (autoriserLeTelechargement.HasValue) query = query.Where(f => f.AutoriserLeTelechargement == autoriserLeTelechargement);

            var fichiers = query.ToList();

            if (minFileSizeMb.HasValue || maxFileSizeMb.HasValue)
            {
                fichiers = fichiers
                    .Where(f =>
                    {
                        var sizeMb = f.TailleOctets / (1024d * 1024d);
                        if (minFileSizeMb.HasValue && sizeMb < minFileSizeMb.Value)
                        {
                            return false;
                        }

                        if (maxFileSizeMb.HasValue && sizeMb > maxFileSizeMb.Value)
                        {
                            return false;
                        }

                        return true;
                    })
                    .ToList();
            }

            var result = new FichierStockeSearchViewModel
            {
                Search = search,
                MinFileSizeMb = minFileSizeMb,
                MaxFileSizeMb = maxFileSizeMb,
                IdCreateur = idCreateur,
                IdVisibilite = idVisibilite,
                IdEntreprise = idEntreprise,
                IdLicence = idLicence,
                AutoriserLeTelechargement = autoriserLeTelechargement,
                Createurs = availableCreators,
                Visibilites = availableVisibilites,
                Entreprises = availableEntreprises,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                Fichiers = fichiers
            };

            return View(result);
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult Upload()
        {
            var visibilites = GetAllowedVisibilites();

            return View(new FichierStockeUploadViewModel
            {
                Visibilites = visibilites,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList(),
                AutoriserLeTelechargement = true
            });
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(FichierStockeUploadViewModel model)
        {
            var visibilites = GetAllowedVisibilites();
            model.Visibilites = visibilites;
            model.Licences = _context.Licence.Where(l => l.Actif).ToList();
            model.TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList();
            var normalizedName = model.Nom?.Trim() ?? string.Empty;

            if (!visibilites.Any(v => v.Id == model.IdVisibilite))
            {
                ModelState.AddModelError(nameof(model.IdVisibilite), "Please select a valid visibility level.");
            }

            if (!model.Licences.Any(l => l.Id == model.IdLicence))
            {
                ModelState.AddModelError(nameof(model.IdLicence), "Please select a valid license.");
            }

            if (model.IdTypeEnergieRenouvelable.HasValue && !model.TypesEnergieRenouvelable.Any(t => t.Id == model.IdTypeEnergieRenouvelable))
            {
                ModelState.AddModelError(nameof(model.IdTypeEnergieRenouvelable), "Please select a valid energy type.");
            }

            if (User.IsInRole("user") && model.IdVisibilite != VisibiliteIds.Personnelle)
            {
                ModelState.AddModelError(nameof(model.IdVisibilite), "Standard users can only upload personal files.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedName))
            {
                var normalizedNameUpper = normalizedName.ToUpper();
                if (_context.FichierStocke.Any(f => f.Nom.ToUpper() == normalizedNameUpper))
                {
                    ModelState.AddModelError(nameof(model.Nom), "A file with this name already exists. Please choose a unique name.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Nom = normalizedName;
                return View(model);
            }

            var file = model.UploadedFile;
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError(nameof(model.UploadedFile), "Please choose a file to upload.");
                return View(model);
            }

            if (file.Length > UploadSizeLimits.StepUploadLimitBytes)
            {
                ModelState.AddModelError(nameof(model.UploadedFile), $"The uploaded file exceeds the maximum size of {UploadSizeLimits.StepUploadLimitDisplay}.");
                return View(model);
            }

            var originalFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalFileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(model.UploadedFile), "This file type is not allowed.");
                return View(model);
            }

            var contentTypeProvider = new FileExtensionContentTypeProvider();
            if (!contentTypeProvider.TryGetContentType(originalFileName, out var resolvedContentType))
            {
                resolvedContentType = "application/octet-stream";
            }
            var storedFileName = $"{Guid.NewGuid():N}{extension}";

            var storageRoot = GetStorageRoot();
            Directory.CreateDirectory(storageRoot);
            var storedPath = Path.Combine(storageRoot, storedFileName);

            try
            {
                var fileHash = await PersistFileAsync(file, storedPath);

                var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
                if (!userId.HasValue)
                {
                    System.IO.File.Delete(storedPath);
                    return Unauthorized();
                }

                var record = new FichierStocke
                {
                    Nom = normalizedName,
                    Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                    IdLicence = model.IdLicence,
                    IdTypeEnergieRenouvelable = model.IdTypeEnergieRenouvelable,
                    AutoriserLeTelechargement = model.AutoriserLeTelechargement,
                    NomFichierOriginal = originalFileName,
                    NomFichierStocke = storedFileName,
                    TypeContenu = resolvedContentType,
                    TailleOctets = file.Length,
                    HashSha256 = fileHash,
                    DateAjout = DateTime.UtcNow,
                    IdVisibilite = model.IdVisibilite,
                    IdUtilisateur = userId.Value
                };

                _context.FichierStocke.Add(record);
                await _context.SaveChangesAsync();

                TempData["Success"] = "File uploaded successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to upload file.");
                if (System.IO.File.Exists(storedPath))
                {
                    System.IO.File.Delete(storedPath);
                }

                ModelState.AddModelError(string.Empty, "An unexpected error occurred while uploading the file.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var fileRecord = await _context.FichierStocke
                .Include(f => f.Utilisateur)
                    .ThenInclude(u => u.Entreprise)
                .Include(f => f.Visibilite)
                .Include(f => f.Licence)
                .Include(f => f.TypeEnergieRenouvelable)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fileRecord == null)
            {
                return NotFound();
            }

            if (!CanCurrentUserAccessFile(fileRecord, out var requiresAuthentication))
            {
                return requiresAuthentication ? Challenge() : Forbid();
            }

            if (!fileRecord.AutoriserLeTelechargement)
            {
                return Forbid();
            }

            var storageRoot = GetStorageRoot();
            var storedPath = Path.Combine(storageRoot, fileRecord.NomFichierStocke);
            if (!System.IO.File.Exists(storedPath))
            {
                return NotFound();
            }

            fileRecord.NombreDeTelechargements += 1;
            await _context.SaveChangesAsync();

            var stream = new FileStream(storedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var safeFileName = Path.GetFileName(fileRecord.NomFichierOriginal);
            var downloadContentType = AllowedExtensions.Contains(Path.GetExtension(safeFileName))
                ? fileRecord.TypeContenu
                : "application/octet-stream";
            return File(stream, downloadContentType, safeFileName);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var fileRecord = await _context.FichierStocke
                .Include(f => f.Utilisateur)
                    .ThenInclude(u => u.Entreprise)
                .Include(f => f.Visibilite)
                .Include(f => f.Licence)
                .Include(f => f.TypeEnergieRenouvelable)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fileRecord == null)
            {
                return NotFound();
            }

            if (!CanCurrentUserAccessFile(fileRecord, out var requiresAuthentication))
            {
                return requiresAuthentication ? Challenge() : Forbid();
            }

            return View(fileRecord);
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public async Task<IActionResult> Edit(int id)
        {
            var fileRecord = await _context.FichierStocke
                .Include(f => f.Visibilite)
                .Include(f => f.Licence)
                .Include(f => f.TypeEnergieRenouvelable)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fileRecord == null)
            {
                return NotFound();
            }

            if (!CanManageFile(fileRecord))
            {
                return Forbid();
            }

            var visibilites = GetAllowedVisibilites();

            var model = new FichierStockeEditViewModel
            {
                Id = fileRecord.Id,
                Nom = fileRecord.Nom,
                Description = fileRecord.Description,
                IdLicence = fileRecord.IdLicence,
                IdTypeEnergieRenouvelable = fileRecord.IdTypeEnergieRenouvelable,
                IdVisibilite = fileRecord.IdVisibilite,
                AutoriserLeTelechargement = fileRecord.AutoriserLeTelechargement,
                Visibilites = visibilites,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(FichierStockeEditViewModel model)
        {
            var fileRecord = await _context.FichierStocke
                .FirstOrDefaultAsync(f => f.Id == model.Id);

            if (fileRecord == null)
            {
                return NotFound();
            }

            if (!CanManageFile(fileRecord))
            {
                return Forbid();
            }

            var visibilites = GetAllowedVisibilites();
            model.Visibilites = visibilites;
            model.Licences = _context.Licence.Where(l => l.Actif).ToList();
            model.TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList();

            if (!visibilites.Any(v => v.Id == model.IdVisibilite))
            {
                ModelState.AddModelError(nameof(model.IdVisibilite), "Please select a valid visibility level.");
            }

            if (!model.Licences.Any(l => l.Id == model.IdLicence))
            {
                ModelState.AddModelError(nameof(model.IdLicence), "Please select a valid license.");
            }

            if (model.IdTypeEnergieRenouvelable.HasValue && !model.TypesEnergieRenouvelable.Any(t => t.Id == model.IdTypeEnergieRenouvelable))
            {
                ModelState.AddModelError(nameof(model.IdTypeEnergieRenouvelable), "Please select a valid energy type.");
            }

            if (User.IsInRole("user") && model.IdVisibilite != VisibiliteIds.Personnelle)
            {
                ModelState.AddModelError(nameof(model.IdVisibilite), "Standard users can only save personal files.");
            }

            if (!ModelState.IsValid)
            {
                model.Nom = fileRecord.Nom;
                return View(model);
            }

            fileRecord.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            fileRecord.IdLicence = model.IdLicence;
            fileRecord.IdTypeEnergieRenouvelable = model.IdTypeEnergieRenouvelable;
            fileRecord.IdVisibilite = model.IdVisibilite;
            fileRecord.AutoriserLeTelechargement = model.AutoriserLeTelechargement;

            await _context.SaveChangesAsync();

            TempData["Success"] = "File details updated successfully.";
            return RedirectToAction(nameof(Details), new { id = fileRecord.Id });
        }

        [HttpGet]
        [Authorize(Roles = "administrator,editor,user")]
        public async Task<IActionResult> Delete(int id)
        {
            var fileRecord = await _context.FichierStocke
                .Include(f => f.Utilisateur)
                    .ThenInclude(u => u.Entreprise)
                .Include(f => f.Visibilite)
                .Include(f => f.Licence)
                .Include(f => f.TypeEnergieRenouvelable)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fileRecord == null)
            {
                return NotFound();
            }

            if (!CanManageFile(fileRecord))
            {
                return Forbid();
            }

            return View(fileRecord);
        }

        [HttpPost]
        [Authorize(Roles = "administrator,editor,user")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fileRecord = await _context.FichierStocke
                .FirstOrDefaultAsync(f => f.Id == id);

            if (fileRecord == null)
            {
                return NotFound();
            }

            if (!CanManageFile(fileRecord))
            {
                return Forbid();
            }

            var storageRoot = GetStorageRoot();
            var storedPath = Path.Combine(storageRoot, fileRecord.NomFichierStocke);
            if (System.IO.File.Exists(storedPath))
            {
                System.IO.File.Delete(storedPath);
            }

            _context.FichierStocke.Remove(fileRecord);
            await _context.SaveChangesAsync();

            TempData["Success"] = "File deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private IQueryable<FichierStocke> ApplyVisibilityFilter(IQueryable<FichierStocke> query)
        {
            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
            var userRole = HttpContextUserHelper.GetCurrentUserRole(HttpContext, _context);
            var userEntrepriseId = HttpContextUserHelper.GetCurrentUserEntrepriseId(HttpContext, _context);

            var isAuthenticated = userId.HasValue;
            var isAdmin = userRole == RoleIds.Administrateur;
            var isInternalRole = userRole == RoleIds.Utilisateur || userRole == RoleIds.Editeur;

            return query.Where(f =>
                f.IdVisibilite == VisibiliteIds.Public ||
                (f.IdVisibilite == VisibiliteIds.Prive && isAuthenticated) ||
                (
                    f.IdVisibilite == VisibiliteIds.Interne &&
                    isAuthenticated &&
                    (
                        isAdmin ||
                        (
                            isInternalRole &&
                            userEntrepriseId.HasValue &&
                            f.Utilisateur != null &&
                            f.Utilisateur.IdEntreprise == userEntrepriseId
                        )
                    )
                ) ||
                (
                    f.IdVisibilite == VisibiliteIds.Personnelle &&
                    (isAdmin || (isAuthenticated && f.IdUtilisateur == userId))
                )
            );
        }

        private bool CanCurrentUserAccessFile(FichierStocke fileRecord, out bool requiresAuthentication)
        {
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            requiresAuthentication = false;

            if (fileRecord.IdVisibilite == VisibiliteIds.Public)
            {
                return true;
            }

            if (!isAuthenticated)
            {
                requiresAuthentication = true;
                return false;
            }

            var role = HttpContextUserHelper.GetCurrentUserRole(HttpContext, _context);
            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);

            return fileRecord.IdVisibilite switch
            {
                VisibiliteIds.Prive => true,
                VisibiliteIds.Interne => role == RoleIds.Administrateur ||
                                        ((role == RoleIds.Utilisateur || role == RoleIds.Editeur) &&
                                         HttpContextUserHelper.GetCurrentUserEntrepriseId(HttpContext, _context) is int entrepriseId &&
                                         fileRecord.Utilisateur != null &&
                                         fileRecord.Utilisateur.IdEntreprise == entrepriseId),
                VisibiliteIds.Personnelle => role == RoleIds.Administrateur ||
                                             (userId.HasValue && fileRecord.IdUtilisateur == userId.Value),
                _ => false
            };
        }

        private bool CanManageFile(FichierStocke fileRecord)
        {
            if (User.IsInRole("administrator") || User.IsInRole("editor"))
            {
                return true;
            }

            var userId = HttpContextUserHelper.TryGetCurrentUserId(User);
            return userId.HasValue && fileRecord.IdUtilisateur == userId.Value;
        }

        private List<Visibilite> GetAllowedVisibilites()
        {
            var visibilites = _context.Visibilite.AsQueryable();
            if (User.IsInRole("user"))
            {
                visibilites = visibilites.Where(v => v.Id == VisibiliteIds.Personnelle);
            }

            return visibilites.ToList();
        }

        private string GetStorageRoot()
        {
            return Path.Combine(_environment.ContentRootPath, "App_Data", StoredFilesFolderName);
        }

        private static async Task<string> PersistFileAsync(IFormFile file, string destinationPath)
        {
            await using var input = file.OpenReadStream();
            await using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var sha256 = SHA256.Create();
            var buffer = new byte[81920];
            int bytesRead;

            while ((bytesRead = await input.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                await output.WriteAsync(buffer.AsMemory(0, bytesRead));
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(sha256.Hash ?? Array.Empty<byte>()).ToLowerInvariant();
        }
    }
}