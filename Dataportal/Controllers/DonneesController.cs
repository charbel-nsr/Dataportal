using Dataportal.Context;
using Dataportal.Models;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
//TODO: fix the multi select aparence

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
        Appareils = _context.Appareil.Where(a => a.Actif).ToList()
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
    }
}