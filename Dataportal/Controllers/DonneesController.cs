using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Data;
//TODO: validate and limite file sizer in import step2, 3 amd 4
//TODO: add a processing page between step2 and step3
//TODO: add a processing page between step3 and step4
//TODO: Support batch inserts for performance.
//TODO: suport diffrent types of files, other then csv
//TODO: do step 3, 4 and 5
//TODO: implement SqlBulkCopy for faster inserts 
//TODO: add confirmatio on each next button
//TODO: control on the dates to be by default the curent date and not bigger then from not biger the the too
//TODO: add at the end of the id a code to describe wich step it is, like data or logs or env event
//TODO: id metadone in step 2 is not being filled
//TODO: allow user to create pivate data and edit the cmnt in the database of is role
//TODO: create a many to many relation in the data page creation to pick this data is intern to wich compani
//TODO: fix error after step 4
//TODO: create tabel for data quality silver, bronze, gold, dimanond
//TODO: automaticly calculate the data size at the end
//TODO: make the control on wich visibilite each user can assign to the data

namespace Dataportal.Controllers
{
    [Authorize(Roles = "administrateur")]
    public class DonneesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DonneesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult CreateStep1()
        {
            var vm = new MetadonneeCreateViewModel
            {
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                Sites = _context.Site.Where(s => s.Actif).ToList(),
                Visibilites = _context.Visibilite.ToList(),
                Appareils = _context.Appareil.Where(a => a.Actif).ToList(),
                AppareilInfos = new List<MetadonneeAppareilInfo>()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateStep1(MetadonneeCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Reload choices
                model.Licences = _context.Licence.Where(l => l.Actif).ToList();
                model.Sites = _context.Site.Where(s => s.Actif).ToList();
                model.Visibilites = _context.Visibilite.ToList();
                model.Appareils = _context.Appareil.Where(a => a.Actif).ToList();
                model.AppareilInfos ??= new List<MetadonneeAppareilInfo>();
                return View(model);
            }

            TempData["Step1Data"] = JsonConvert.SerializeObject(model);

            return RedirectToAction("CreateStep2");
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("UserId claim missing.");

            return int.Parse(userIdClaim);
        }

        [HttpGet]
        public IActionResult CreateStep2()
        {
            var step1Json = TempData.Peek("Step1Data") as string;
            if (string.IsNullOrEmpty(step1Json))
            {
                TempData["Error"] = "Vous devez d'abord remplir la première étape.";
                return RedirectToAction("CreateStep1");
            }

            // show the upload + Donnees form
            var vm = new DonneesCreateStep2ViewModel();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep2(DonneesCreateStep2ViewModel model)
        {
            var step1Json = TempData["Step1Data"] as string;
            if (string.IsNullOrEmpty(step1Json))
            {
                TempData["Error"] = "Les informations de la première étape sont manquantes.";
                return RedirectToAction("CreateStep1");
            }

            var step1Data = JsonConvert.DeserializeObject<MetadonneeCreateViewModel>(step1Json);

            if (!ModelState.IsValid)
            {
                TempData.Keep("Step1Data");
                return View(model);
            }

            var duplicate = await _context.Donnees
                .FirstOrDefaultAsync(d =>
                    d.Libelle.ToLower() == model.Libelle.Trim().ToLower() ||
                    d.Code.ToLower() == model.Code.Trim().ToLower());

            if (duplicate != null)
            {
                if (duplicate.Libelle.Equals(model.Libelle.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Libelle", "Ce libellé existe déjà.");
                if (duplicate.Code.Equals(model.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Code", "Ce code existe déjà.");

                TempData.Keep("Step1Data");
                return View(model);
            }

            if (model.UploadedFiles == null || !model.UploadedFiles.Any())
            {
                ModelState.AddModelError("UploadedFiles", "Vous devez importer au moins un fichier CSV.");
                TempData.Keep("Step1Data");
                return View(model);
            }

            // -- Show optional processing page here if you want
            // return View("Processing");

            // Merge CSVs
            var mergedData = new DataTable();
            foreach (var file in model.UploadedFiles)
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);
                var csv = await reader.ReadToEndAsync();
                var table = CsvToDataTable(csv);

                if (mergedData.Columns.Count == 0)
                {
                    mergedData = table;
                }
                else
                {
                    if (!ColumnsMatch(mergedData, table))
                    {
                        ModelState.AddModelError("", "Les fichiers CSV doivent avoir les mêmes colonnes.");
                        TempData.Keep("Step1Data");
                        return View(model);
                    }
                    foreach (DataRow row in table.Rows)
                    {
                        mergedData.ImportRow(row);
                    }
                }
            }

            // Create SQL table
            var tableName = $"Donnees.{model.Libelle}-{model.Code}".Replace(" ", "_");
            await CreateSqlTableFromDataTable(tableName, mergedData);

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
                EndTimestamp = model.EndTimestamp
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
                TailleDesDonnees = step1Data.TailleDesDonnees?.Trim(),
                SeriesTemporelles = step1Data.SeriesTemporelles,
                AutoriserApi = step1Data.AutoriserApi,
                Anonymiser = step1Data.Anonymiser,
                AutoriserLeTelechargement = step1Data.AutoriserLeTelechargement,
                IdUtilisateur = GetCurrentUserId(),
                DernierMiseAJour = DateTime.Now,
                NombreDeTelechargements = 0,
                QualiteDesDonnees = 0,
                IdDonnees = donnees.Id
            };

            _context.Metadonnee.Add(metadonnee);
            await _context.SaveChangesAsync();

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
                await _context.SaveChangesAsync();
            }

            // Clear Step1Data from TempData
            TempData.Remove("Step1Data");

            return RedirectToAction("CreateStep3", new { id = metadonnee.Id });
        }

        private DataTable CsvToDataTable(string csvContent)
        {
            var dt = new DataTable();
            using var reader = new StringReader(csvContent);
            var header = reader.ReadLine()?.Split(',');
            if (header == null) throw new Exception("CSV vide.");

            foreach (var column in header)
            {
                dt.Columns.Add(column.Trim());
            }

            while (reader.Peek() > -1)
            {
                var line = reader.ReadLine();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    dt.Rows.Add(line.Split(','));
                }
            }

            return dt;
        }
         
        private bool ColumnsMatch(DataTable dt1, DataTable dt2)
        {
            if (dt1.Columns.Count != dt2.Columns.Count) return false;
            for (int i = 0; i < dt1.Columns.Count; i++)
            {
                if (!dt1.Columns[i].ColumnName.Equals(dt2.Columns[i].ColumnName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        private async Task CreateSqlTableFromDataTable(string tableName, DataTable data)
        {
            using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            var columnDefs = string.Join(", ", data.Columns
                .Cast<DataColumn>()
                .Select(c => $"[{c.ColumnName}] NVARCHAR(MAX)"));
            // Check if 'id' column exists
            bool hasIdColumn = data.Columns.Cast<DataColumn>().Any(c => string.Equals(c.ColumnName, "id", StringComparison.OrdinalIgnoreCase));

            string createTableCmd;
            if (hasIdColumn)
            {
                // Use CSV-provided 'id'
                createTableCmd = $"CREATE TABLE [DataPortal].[dbo].[{tableName}] ({columnDefs})";
            }
            else
            {
                // Add our own IDENTITY
                createTableCmd = $"CREATE TABLE [DataPortal].[dbo].[{tableName}] (Id INT IDENTITY(1,1) PRIMARY KEY, {columnDefs})";
            }

            using (var cmd = new SqlCommand(createTableCmd, connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Batch insert
            foreach (DataRow row in data.Rows)
            {
                var columns = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"[{c.ColumnName}]"));
                var values = string.Join(", ", data.Columns.Cast<DataColumn>().Select(c => $"@{c.ColumnName}"));

                var insertCmdText = $"INSERT INTO [{tableName}] ({columns}) VALUES ({values})";
                using var insertCmd = new SqlCommand(insertCmdText, connection);
                foreach (DataColumn col in data.Columns)
                {
                    insertCmd.Parameters.AddWithValue($"@{col.ColumnName}", row[col.ColumnName] ?? DBNull.Value);
                }
                await insertCmd.ExecuteNonQueryAsync();
            }
        }

        [HttpGet]
        public IActionResult CreateStep3(int id)
        {
            // Validate Metadonnee exists
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            var vm = new DonneesEventLogsCreateStep3ViewModel
            {
                IdMetadonnee = id
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep3(DonneesEventLogsCreateStep3ViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var metadonnee = await _context.Metadonnee.FindAsync(model.IdMetadonnee);
            if (metadonnee == null)
            {
                TempData["Error"] = "Métadonnée introuvable.";
                return RedirectToAction("CreateStep1");
            }

            // Handle skip: if no file uploaded, user wants to skip
            if (model.UploadedFiles == null || !model.UploadedFiles.Any())
            {
                return RedirectToAction("CreateStep4", new { id = model.IdMetadonnee });
            }

            var duplicate = await _context.DonneesEventLogs.FirstOrDefaultAsync(e =>
                            e.Libelle.ToLower() == model.Libelle.Trim().ToLower() ||
                            e.Code.ToLower() == model.Code.Trim().ToLower());

            if (duplicate != null)
            {
                if (duplicate.Libelle.Equals(model.Libelle.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Libelle", "Ce libellé existe déjà.");
                if (duplicate.Code.Equals(model.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Code", "Ce code existe déjà.");

                return View(model);
            }

            // Merge uploaded CSVs
            var mergedData = new DataTable();
            foreach (var file in model.UploadedFiles)
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);
                var csv = await reader.ReadToEndAsync();
                var table = CsvToDataTable(csv);

                if (mergedData.Columns.Count == 0)
                {
                    mergedData = table;
                }
                else
                {
                    if (!ColumnsMatch(mergedData, table))
                    {
                        TempData["Error"] = "Les fichiers CSV doivent avoir les mêmes colonnes.";
                        return View(model);
                    }
                    foreach (DataRow row in table.Rows)
                    {
                        mergedData.ImportRow(row);
                    }
                }
            }

            // Create SQL Table
            var tableName = $"DonneesEventLogs.{model.Libelle}-{model.Code}".Replace(" ", "_");
            await CreateSqlTableFromDataTable(tableName, mergedData);

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
                IdMetadonnee = model.IdMetadonnee
            };

            _context.DonneesEventLogs.Add(eventLogs);
            await _context.SaveChangesAsync();

            // Link to Metadonnee
            metadonnee.IdDonneesEventLogs = eventLogs.Id;
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            return RedirectToAction("CreateStep4", new { id = model.IdMetadonnee });
        }

        // GET: /Donnees/CreateStep4/{id}
        [HttpGet]
        public IActionResult CreateStep4(int id)
        {
            var metadonnee = _context.Metadonnee.Find(id);
            if (metadonnee == null)
            {
                return NotFound();
            }

            var vm = new DonneesContexteEnvironnementalCreateStep4ViewModel
            {
                IdMetadonnee = id
            };
            return View(vm);
        }

        // POST: /Donnees/CreateStep4
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStep4(DonneesContexteEnvironnementalCreateStep4ViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var metadonnee = await _context.Metadonnee.FindAsync(model.IdMetadonnee);
            if (metadonnee == null)
            {
                TempData["Error"] = "Métadonnée introuvable.";
                return RedirectToAction("CreateStep1");
            }

            if (model.UploadedFiles == null || !model.UploadedFiles.Any())
            {
                // User skipped uploading: go straight to next step
                return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
            }

            var duplicate = await _context.DonneesContexteEnvironnemental.FirstOrDefaultAsync(c =>
                            c.Libelle.ToLower() == model.Libelle.Trim().ToLower() ||
                            c.Code.ToLower() == model.Code.Trim().ToLower());

            if (duplicate != null)
            {
                if (duplicate.Libelle.Equals(model.Libelle.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Libelle", "Ce libellé existe déjà.");
                if (duplicate.Code.Equals(model.Code.Trim(), StringComparison.OrdinalIgnoreCase))
                    ModelState.AddModelError("Code", "Ce code existe déjà.");

                return View(model);
            }

            // Merge uploaded CSV files
            var mergedData = new DataTable();
            foreach (var file in model.UploadedFiles)
            {
                using var stream = file.OpenReadStream();
                using var reader = new StreamReader(stream);
                var csv = await reader.ReadToEndAsync();
                var table = CsvToDataTable(csv);

                if (mergedData.Columns.Count == 0)
                {
                    mergedData = table;
                }
                else
                {
                    if (!ColumnsMatch(mergedData, table))
                    {
                        TempData["Error"] = "Les fichiers CSV doivent avoir les mêmes colonnes.";
                        return View(model);
                    }
                    foreach (DataRow row in table.Rows)
                    {
                        mergedData.ImportRow(row);
                    }
                }
            }

            // Create SQL Table
            var tableName = $"DonneesContexteEnvironnemental.{model.Libelle}-{model.Code}".Replace(" ", "_");
            await CreateSqlTableFromDataTable(tableName, mergedData);

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
                IdMetadonnee = metadonnee.Id
            };

            _context.DonneesContexteEnvironnemental.Add(donneesContext);
            await _context.SaveChangesAsync();

            // Link to Metadonnee
            metadonnee.IdDonneesContexteEnvironnemental = donneesContext.Id;
            _context.Update(metadonnee);
            await _context.SaveChangesAsync();

            // Proceed to next step
            return RedirectToAction("Details", new { id = metadonnee.Id, creation = true });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, bool? creation)
        {
            // 1️⃣ Load Metadonnee + navigation
            var metadonnee = await _context.Metadonnee
                .Include(m => m.Licence)
                .Include(m => m.Site)
                .Include(m => m.Visibilite)
                .Include(m => m.Utilisateur)
                .Include(m => m.Metadonnee_Appareils).ThenInclude(ma => ma.Appareil)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (metadonnee == null)
                return NotFound();

            // 2️⃣ Load Donnees / EventLogs / Contexte
            var donnees = await _context.Donnees.FirstOrDefaultAsync(d => d.Id == metadonnee.IdDonnees);
            var eventLogs = await _context.DonneesEventLogs.FirstOrDefaultAsync(e => e.Id == metadonnee.IdDonneesEventLogs);
            var contexte = await _context.DonneesContexteEnvironnemental.FirstOrDefaultAsync(c => c.Id == metadonnee.IdDonneesContexteEnvironnemental);

            if (creation == true)
            {
                ViewData["CurrentStep"] = 5;
            }

            // 3️⃣ Load preview data
            var donneesPreview = donnees?.NomDeLaTable != null ? await GetTablePreviewRows(donnees.NomDeLaTable) : null;
            var eventLogsPreview = eventLogs?.NomDeLaTable != null ? await GetTablePreviewRows(eventLogs.NomDeLaTable) : null;
            var contextePreview = contexte?.NomDeLaTable != null ? await GetTablePreviewRows(contexte.NomDeLaTable) : null;

            // 4️⃣ Build ViewModel
            var vm = new MetadonneeDetailsViewModel
            {
                Metadonnee = metadonnee,
                Licence = metadonnee.Licence,
                Site = metadonnee.Site,
                Visibilite = metadonnee.Visibilite,
                Utilisateur = metadonnee.Utilisateur,
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
    }
}