using Dataportal.Classes;
using Dataportal.Context;
using Dataportal.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
            bool? hasContextEnv,
            bool? autoriserApi,
            double? minDataSizeMb,
            double? maxDataSizeMb,
            int? idCreateur,
            int? idVisibilite,
            int? idEntreprise)
        {
            var baseQuery = _context.Metadonnee
                .Include(m => m.Licence)
                .Include(m => m.Site)
                .Include(m => m.Visibilite)
                .Include(m => m.TypeEnergieRenouvelable)
                .Include(m => m.Utilisateur)
                    .ThenInclude(u => u.Entreprise)
                .Include(m => m.Donnees)
                .Include(m => m.DonneesEventLogs)
                .Include(m => m.DonneesContexteEnvironnemental)
                .AsQueryable();

            // Visibilite filtering
            var userId = GetCurrentUserId();
            var userRole = GetCurrentUserRole();
            var userEntrepriseId = GetCurrentUserEntrepriseId();

            var isAuthenticated = userId.HasValue;
            var isAdmin = userRole == RoleIds.Administrateur;
            var isInternalRole = userRole == RoleIds.Utilisateur || userRole == RoleIds.Editeur;

            baseQuery = baseQuery.Where(m =>
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

            var availableCreators = baseQuery
                .Where(m => m.Utilisateur != null)
                .Select(m => new { m.IdUtilisateur, m.Utilisateur.Prenom, m.Utilisateur.Nom })
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
                .Select(m => new { m.IdVisibilite, m.Visibilite.Libelle })
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
                .Where(m => m.Utilisateur != null && m.Utilisateur.Entreprise != null)
                .Select(m => new { m.Utilisateur.Entreprise.Id, m.Utilisateur.Entreprise.Nom })
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
                query = query.Where(m => m.Nom.Contains(search) || m.Description.Contains(search));
            }

            if (idLicence.HasValue) query = query.Where(m => m.IdLicence == idLicence);
            if (idTypeEnergieRenouvelable.HasValue) query = query.Where(m => m.IdTypeEnergieRenouvelable == idTypeEnergieRenouvelable);
            if (seriesTemporelles.HasValue) query = query.Where(m => m.SeriesTemporelles == seriesTemporelles);
            if (anonymiser.HasValue) query = query.Where(m => m.Anonymiser == anonymiser);
            if (autoriserLeTelechargement.HasValue) query = query.Where(m => m.AutoriserLeTelechargement == autoriserLeTelechargement);
            if (hasEventLogs.HasValue && hasEventLogs.Value) query = query.Where(m => m.IdDonneesEventLogs != null);
            if (hasContextEnv.HasValue && hasContextEnv.Value) query = query.Where(m => m.IdDonneesContexteEnvironnemental != null);
            if (autoriserApi.HasValue && autoriserApi.Value) query = query.Where(m => m.AutoriserApi);
            if (idCreateur.HasValue) query = query.Where(m => m.IdUtilisateur == idCreateur);
            if (idVisibilite.HasValue) query = query.Where(m => m.IdVisibilite == idVisibilite);
            if (idEntreprise.HasValue) query = query.Where(m => m.Utilisateur != null && m.Utilisateur.IdEntreprise == idEntreprise);

            var metadonneesList = query.ToList();

            if (minDataSizeMb.HasValue || maxDataSizeMb.HasValue)
            {
                metadonneesList = metadonneesList
                    .Where(m =>
                    {
                        var sizeMb = ParseDataSizeToMb(m.TailleDesDonnees);
                        if (!sizeMb.HasValue)
                        {
                            return false;
                        }

                        if (minDataSizeMb.HasValue && sizeMb.Value < minDataSizeMb.Value)
                        {
                            return false;
                        }

                        if (maxDataSizeMb.HasValue && sizeMb.Value > maxDataSizeMb.Value)
                        {
                            return false;
                        }

                        return true;
                    })
                    .ToList();
            }

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
                AutoriserApi = autoriserApi,
                MinDataSizeMb = minDataSizeMb,
                MaxDataSizeMb = maxDataSizeMb,
                IdCreateur = idCreateur,
                IdVisibilite = idVisibilite,
                IdEntreprise = idEntreprise,
                Licences = _context.Licence.Where(l => l.Actif).ToList(),
                TypesEnergieRenouvelable = _context.TypeEnergieRenouvelable.OrderBy(t => t.Libelle).ToList(),
                Createurs = availableCreators,
                Visibilites = availableVisibilites,
                Entreprises = availableEntreprises,
                Metadonnees = metadonneesList
            };
            return View(result);
        }

        private static double? ParseDataSizeToMb(string? sizeText)
        {
            if (string.IsNullOrWhiteSpace(sizeText))
            {
                return null;
            }

            var parts = sizeText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return null;
            }

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return null;
            }

            var unit = parts[1].Trim().ToUpperInvariant();

            return unit switch
            {
                "B" => value / (1024.0 * 1024.0),
                "KB" => value / 1024.0,
                "MB" => value,
                "GB" => value * 1024.0,
                "TB" => value * 1024.0 * 1024.0,
                _ => null
            };
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
