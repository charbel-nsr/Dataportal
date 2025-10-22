using Microsoft.AspNetCore.Mvc;
using Dataportal.Context;
using Dataportal.ViewModels;
using Dataportal.Models;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Dataportal.Controllers
{
    [Authorize(Roles = "administrateur,editeur")]
    public class ControleController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ControleController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Controle/Sites
        [HttpGet]
        public IActionResult Sites(string search, bool? actif)
        {
            var query = _context.Site.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(s =>
                    s.Nom.Contains(search) ||
                    s.Description.Contains(search) ||
                    s.Emplacement.Contains(search));
            }

            if (actif.HasValue)
            {
                query = query.Where(s => s.Actif == actif.Value);
            }

            var vm = new SiteViewModel
            {
                Sites = query.ToList(),
                Search = search,
                Actif = actif
            };

            return View(vm);
        }

        // POST: /Controle/CreateSite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateSite(string Nom, string Description, string Emplacement, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Site name is required.";
                return RedirectToAction("Sites");
            }
            if (string.IsNullOrWhiteSpace(Description))
            {
                TempData["Error"] = "Description is required.";
                return RedirectToAction("Sites");
            }
            if (string.IsNullOrWhiteSpace(Emplacement))
            {
                TempData["Error"] = "Location is required.";
                return RedirectToAction("Sites");
            }

            var normalized = Nom.Trim().ToLower();
            var exists = _context.Site.Any(s => s.Nom.Trim().ToLower() == normalized);
            if (exists)
            {
                TempData["Error"] = "A site with this name already exists.";
                return RedirectToAction("Sites");
            }

            var site = new Site
            {
                Nom = Nom.Trim(),
                Description = Description?.Trim(),
                Emplacement = Emplacement?.Trim(),
                Actif = Actif
            };

            _context.Site.Add(site);
            _context.SaveChanges();

            TempData["Success"] = "Site created successfully.";
            return RedirectToAction("Sites");
        }

        // POST: /Controle/ActivateSite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateSite(int id)
        {
            var site = _context.Site.FirstOrDefault(s => s.Id == id);
            if (site == null)
            {
                TempData["Error"] = "Site not found.";
                return RedirectToAction("Sites");
            }

            site.Actif = true;
            _context.SaveChanges();

            TempData["Success"] = "Site activated successfully.";
            return RedirectToAction("Sites");
        }

        // POST: /Controle/DeactivateSite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeactivateSite(int id)
        {
            var site = _context.Site.FirstOrDefault(s => s.Id == id);
            if (site == null)
            {
                TempData["Error"] = "Site not found.";
                return RedirectToAction("Sites");
            }

            site.Actif = false;
            _context.SaveChanges();

            TempData["Success"] = "Site deactivated successfully.";
            return RedirectToAction("Sites");
        }

        // POST: /Controle/EditSite
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditSite(int Id, string Nom, string Description, string Emplacement, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Name is required.";
                return RedirectToAction("Sites");
            }

            var site = _context.Site.FirstOrDefault(s => s.Id == Id);
            if (site == null)
            {
                TempData["Error"] = "Site not found.";
                return RedirectToAction("Sites");
            }

            var normalizedNom = Nom.Trim().ToLower();
            var duplicate = _context.Site.Any(s => s.Id != Id && s.Nom.Trim().ToLower() == normalizedNom);
            if (duplicate)
            {
                TempData["Error"] = "Another site with this name already exists.";
                return RedirectToAction("Sites");
            }

            site.Nom = Nom.Trim();
            site.Description = Description?.Trim();
            site.Emplacement = Emplacement?.Trim();
            site.Actif = Actif;

            _context.SaveChanges();

            TempData["Success"] = "Site updated successfully.";
            return RedirectToAction("Sites");
        }

        // GET: /Controle/Licences
        [HttpGet]
        public IActionResult Licences(string search, bool? actif)
        {
            var query = _context.Licence.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(l =>
                    l.Libelle.Contains(search) ||
                    l.Description.Contains(search));
            }

            if (actif.HasValue)
            {
                query = query.Where(l => l.Actif == actif.Value);
            }

            var vm = new LicenceViewModel
            {
                Licences = query.ToList(),
                Search = search,
                Actif = actif
            };

            return View(vm);
        }

        // POST: /Controle/CreateLicence
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateLicence(string Libelle, string Description, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Libelle))
            {
                TempData["Error"] = "Label is required.";
                return RedirectToAction("Licences");
            }

            var normalized = Libelle.Trim().ToLower();
            var exists = _context.Licence.Any(l => l.Libelle.Trim().ToLower() == normalized);
            if (exists)
            {
                TempData["Error"] = "A license with this label already exists.";
                return RedirectToAction("Licences");
            }

            var licence = new Licence
            {
                Libelle = Libelle.Trim(),
                Description = Description?.Trim(),
                Actif = Actif
            };

            _context.Licence.Add(licence);
            _context.SaveChanges();

            TempData["Success"] = "License created successfully.";
            return RedirectToAction("Licences");
        }

        // POST: /Controle/ActivateLicence
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateLicence(int id)
        {
            var licence = _context.Licence.FirstOrDefault(l => l.Id == id);
            if (licence == null)
            {
                TempData["Error"] = "License not found.";
                return RedirectToAction("Licences");
            }

            licence.Actif = true;
            _context.SaveChanges();

            TempData["Success"] = "License activated successfully.";
            return RedirectToAction("Licences");
        }

        // POST: /Controle/DeactivateLicence
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeactivateLicence(int id)
        {
            var licence = _context.Licence.FirstOrDefault(l => l.Id == id);
            if (licence == null)
            {
                TempData["Error"] = "License not found.";
                return RedirectToAction("Licences");
            }

            licence.Actif = false;
            _context.SaveChanges();

            TempData["Success"] = "License deactivated successfully.";
            return RedirectToAction("Licences");
        }

        // POST: /Controle/EditLicence
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditLicence(int Id, string Libelle, string Description, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Libelle))
            {
                TempData["Error"] = "Label is required.";
                return RedirectToAction("Licences");
            }

            var licence = _context.Licence.FirstOrDefault(l => l.Id == Id);
            if (licence == null)
            {
                TempData["Error"] = "License not found.";
                return RedirectToAction("Licences");
            }

            var normalizedLibelle = Libelle.Trim().ToLower();
            var duplicate = _context.Licence.Any(l => l.Id != Id && l.Libelle.Trim().ToLower() == normalizedLibelle);
            if (duplicate)
            {
                TempData["Error"] = "Another license with this label already exists.";
                return RedirectToAction("Licences");
            }

            licence.Libelle = Libelle.Trim();
            licence.Description = Description?.Trim();
            licence.Actif = Actif;

            _context.SaveChanges();

            TempData["Success"] = "License updated successfully.";
            return RedirectToAction("Licences");
        }

        // GET: /Controle/Appareils
        [HttpGet]
        public IActionResult Appareils(string search, bool? actif)
        {
            var query = _context.Appareil.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a =>
                    a.Nom.Contains(search) ||
                    a.Description.Contains(search) ||
                    a.Capacite.Contains(search) ||
                    a.Model.Contains(search) ||
                    a.Manufacturer.Contains(search));
            }

            if (actif.HasValue)
            {
                query = query.Where(a => a.Actif == actif.Value);
            }

            var vm = new AppareilViewModel
            {
                Appareils = query.ToList(),
                Search = search,
                Actif = actif
            };

            return View(vm);
        }

        // POST: /Controle/CreateAppareil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAppareil(string Nom, string Description, string Capacite, string Model, string Manufacturer, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Name is required.";
                return RedirectToAction("Appareils");
            }

            var normalized = Nom.Trim().ToLower();
            var exists = _context.Appareil.Any(a => a.Nom.Trim().ToLower() == normalized);
            if (exists)
            {
                TempData["Error"] = "A device with this name already exists.";
                return RedirectToAction("Appareils");
            }

            var appareil = new Appareil
            {
                Nom = Nom.Trim(),
                Description = Description?.Trim(),
                Capacite = Capacite?.Trim(),
                Model = Model?.Trim(),
                Manufacturer = Manufacturer?.Trim(),
                Actif = Actif
            };

            _context.Appareil.Add(appareil);
            _context.SaveChanges();

            TempData["Success"] = "Device created successfully.";
            return RedirectToAction("Appareils");
        }

        // POST: /Controle/ActivateAppareil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateAppareil(int id)
        {
            var appareil = _context.Appareil.FirstOrDefault(a => a.Id == id);
            if (appareil == null)
            {
                TempData["Error"] = "Device not found.";
                return RedirectToAction("Appareils");
            }

            appareil.Actif = true;
            _context.SaveChanges();

            TempData["Success"] = "Device activated successfully.";
            return RedirectToAction("Appareils");
        }

        // POST: /Controle/DeactivateAppareil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeactivateAppareil(int id)
        {
            var appareil = _context.Appareil.FirstOrDefault(a => a.Id == id);
            if (appareil == null)
            {
                TempData["Error"] = "Device not found.";
                return RedirectToAction("Appareils");
            }

            appareil.Actif = false;
            _context.SaveChanges();

            TempData["Success"] = "Device deactivated successfully.";
            return RedirectToAction("Appareils");
        }

        // POST: /Controle/EditAppareil
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditAppareil(int Id, string Nom, string Description, string Capacite, string Model, string Manufacturer, bool Actif)
        {
            if (string.IsNullOrWhiteSpace(Nom))
            {
                TempData["Error"] = "Name is required.";
                return RedirectToAction("Appareils");
            }

            var appareil = _context.Appareil.FirstOrDefault(a => a.Id == Id);
            if (appareil == null)
            {
                TempData["Error"] = "Device not found.";
                return RedirectToAction("Appareils");
            }

            // Check for duplicate name
            var normalizedNom = Nom.Trim().ToLower();
            var duplicate = _context.Appareil.Any(a => a.Id != Id && a.Nom.Trim().ToLower() == normalizedNom);
            if (duplicate)
            {
                TempData["Error"] = "Another device with this name already exists.";
                return RedirectToAction("Appareils");
            }

            appareil.Nom = Nom.Trim();
            appareil.Description = Description?.Trim();
            appareil.Capacite = Capacite?.Trim();
            appareil.Model = Model?.Trim();
            appareil.Manufacturer = Manufacturer?.Trim();
            appareil.Actif = Actif;

            _context.SaveChanges();

            TempData["Success"] = "Device updated successfully.";
            return RedirectToAction("Appareils");
        }
    }
}
