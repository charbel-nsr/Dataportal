using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dataportal.Controllers
{
    [AllowAnonymous]
    public class AccesDonneesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccesDonneesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult RechercheDonnees(
            string? search,
            int? idLicence,
            int? idSite,
            bool? seriesTemporelles,
            bool? anonymiser,
            bool? autoriserLeTelechargement,
            bool? hasEventLogs,
            bool? hasContextEnv)
        {
            var query = _context.Metadonnee
                .Include(m => m.Licence)
                .Include(m => m.Site)
                .Include(m => m.Visibilite)
                .Include(m => m.Utilisateur)
                .AsQueryable();

            // Visibilite filtering
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var userEntrepriseId = GetCurrentUserEntrepriseId();

            query = query.Where(m =>
                m.IdVisibilite == VisibiliteIds.Public ||
                (m.IdVisibilite == VisibiliteIds.Prive && userId != null) ||
                (m.IdVisibilite == VisibiliteIds.Interne && userId != null && m.Utilisateur.IdEntreprise == userEntrepriseId) ||
                (m.IdVisibilite == VisibiliteIds.Personnelle && m.IdUtilisateur == userId)
            );

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.Nom.Contains(search) || m.Description.Contains(search));
            }

            if (idLicence.HasValue) query = query.Where(m => m.IdLicence == idLicence);
            if (idSite.HasValue) query = query.Where(m => m.IdSite == idSite);
            if (seriesTemporelles.HasValue) query = query.Where(m => m.SeriesTemporelles == seriesTemporelles);
            if (anonymiser.HasValue) query = query.Where(m => m.Anonymiser == anonymiser);
            if (autoriserLeTelechargement.HasValue) query = query.Where(m => m.AutoriserLeTelechargement == autoriserLeTelechargement);
            if (hasEventLogs.HasValue && hasEventLogs.Value) query = query.Where(m => m.IdDonneesEventLogs != null);
            if (hasContextEnv.HasValue && hasContextEnv.Value) query = query.Where(m => m.IdDonneesContexteEnvironnemental != null);

            var result = new RechercheDonneesViewModel
            {
                Search = search,
                IdLicence = idLicence,
                IdSite = idSite,
                SeriesTemporelles = seriesTemporelles,
                Anonymiser = anonymiser,
                AutoriserLeTelechargement = autoriserLeTelechargement,
                HasEventLogs = hasEventLogs,
                HasContextEnv = hasContextEnv,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                Sites = _context.Site.Where(s => s.Actif).ToList(),
                Metadonnees = query.ToList()
            };

            return View(result);
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst("UserId")?.Value;
            return claim != null ? int.Parse(claim) : (int?)null;
        }

        private int? GetCurrentUserEntrepriseId()
        {
            var claim = User.FindFirst("EntrepriseId")?.Value;
            return claim != null ? int.Parse(claim) : (int?)null;
        }

        private int? GetCurrentUserRole()
        {
            var claim = User.FindFirst("RoleId")?.Value;
            return claim != null ? int.Parse(claim) : (int?)null;
        }
    }

}
