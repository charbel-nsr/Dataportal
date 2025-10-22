using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
//TODO: add API
//TODO: java translate FR/EN
//TODO: add limitation to tabel line number

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
            int? idTypeEnergieRenouvelable,
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
                .Include(m => m.TypeEnergieRenouvelable)
                .Include(m => m.Utilisateur)
                    .ThenInclude(u => u.Entreprise)
                .AsQueryable();

            // Visibilite filtering
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var userEntrepriseId = GetCurrentUserEntrepriseId();

            var isAuthenticated = userId.HasValue;
            var isAdmin = userRole == RoleIds.Administrateur;
            var isInternalRole = userRole == RoleIds.Utilisateur || userRole == RoleIds.Editeur;

            query = query.Where(m =>
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
                )
            );

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(m => m.Nom.Contains(search) || m.Description.Contains(search));
            }

            if (idLicence.HasValue) query = query.Where(m => m.IdLicence == idLicence);
            if (idTypeEnergieRenouvelable.HasValue) query = query.Where(m => m.IdTypeEnergieRenouvelable == idTypeEnergieRenouvelable);
            if (seriesTemporelles.HasValue) query = query.Where(m => m.SeriesTemporelles == seriesTemporelles);
            if (anonymiser.HasValue) query = query.Where(m => m.Anonymiser == anonymiser);
            if (autoriserLeTelechargement.HasValue) query = query.Where(m => m.AutoriserLeTelechargement == autoriserLeTelechargement);
            if (hasEventLogs.HasValue && hasEventLogs.Value) query = query.Where(m => m.IdDonneesEventLogs != null);
            if (hasContextEnv.HasValue && hasContextEnv.Value) query = query.Where(m => m.IdDonneesContexteEnvironnemental != null);

            var result = new RechercheDonneesViewModel
            {
                Search = search,
                IdLicence = idLicence,
                IdTypeEnergieRenouvelable = idTypeEnergieRenouvelable,
                SeriesTemporelles = seriesTemporelles,
                Anonymiser = anonymiser,
                AutoriserLeTelechargement = autoriserLeTelechargement,
                HasEventLogs = hasEventLogs,
                HasContextEnv = hasContextEnv,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList(),
                Metadonnees = query.ToList()
            };

            return View(result);
        }

        private int? GetCurrentUserId()
        {
            var claim = User.FindFirst("UserId")?.Value;
            return int.TryParse(claim, out var id) ? id : (int?)null;
        }

        private int? GetCurrentUserEntrepriseId()
        {
            if (HttpContext.Items.TryGetValue(nameof(GetCurrentUserEntrepriseId), out var cached) && cached is int cachedId)
            {
                return cachedId;
            }

            var claim = User.FindFirst("EntrepriseId")?.Value;
            if (int.TryParse(claim, out var entrepriseId))
            {
                HttpContext.Items[nameof(GetCurrentUserEntrepriseId)] = entrepriseId;
                return entrepriseId;
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return null;
            }

            var resolvedEntrepriseId = _context.Utilisateur
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => (int?)u.IdEntreprise)
                .FirstOrDefault();

            if (resolvedEntrepriseId.HasValue)
            {
                HttpContext.Items[nameof(GetCurrentUserEntrepriseId)] = resolvedEntrepriseId.Value;
            }

            return resolvedEntrepriseId;
        }

        private int? GetCurrentUserRole()
        {
            if (HttpContext.Items.TryGetValue(nameof(GetCurrentUserRole), out var cached) && cached is int cachedRole)
            {
                return cachedRole;
            }

            var claim = User.FindFirst("RoleId")?.Value;
            if (int.TryParse(claim, out var roleId))
            {
                HttpContext.Items[nameof(GetCurrentUserRole)] = roleId;
                return roleId;
            }

            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return null;
            }

            var resolvedRoleId = _context.Utilisateur
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => (int?)u.IdRole)
                .FirstOrDefault();

            if (resolvedRoleId.HasValue)
            {
                HttpContext.Items[nameof(GetCurrentUserRole)] = resolvedRoleId.Value;
            }

            return resolvedRoleId;
        }
    }

}
