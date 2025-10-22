using Dataportal.Context;
using Dataportal.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;

namespace Dataportal.Classes
{
    public static class HttpContextUserHelper
    {
        private static readonly object RoleCacheKey = new object();
        private static readonly object EntrepriseCacheKey = new object();

        public static int? TryGetCurrentUserId(ClaimsPrincipal user)
        {
            var claim = user.FindFirst("UserId")?.Value;
            return int.TryParse(claim, out var id) ? id : (int?)null;
        }

        public static int? GetCurrentUserRole(HttpContext httpContext, ApplicationDbContext dbContext)
        {
            if (httpContext.Items.TryGetValue(RoleCacheKey, out var cached) && cached is int cachedRole)
            {
                return cachedRole;
            }

            var claim = httpContext.User.FindFirst("RoleId")?.Value;
            if (int.TryParse(claim, out var roleId))
            {
                httpContext.Items[RoleCacheKey] = roleId;
                return roleId;
            }

            var userId = TryGetCurrentUserId(httpContext.User);
            if (!userId.HasValue)
            {
                return null;
            }

            var resolvedRoleId = dbContext.Utilisateur
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => (int?)u.IdRole)
                .FirstOrDefault();

            if (resolvedRoleId.HasValue)
            {
                httpContext.Items[RoleCacheKey] = resolvedRoleId.Value;
            }

            return resolvedRoleId;
        }

        public static int? GetCurrentUserEntrepriseId(HttpContext httpContext, ApplicationDbContext dbContext)
        {
            if (httpContext.Items.TryGetValue(EntrepriseCacheKey, out var cached) && cached is int cachedEntreprise)
            {
                return cachedEntreprise;
            }

            var claim = httpContext.User.FindFirst("EntrepriseId")?.Value;
            if (int.TryParse(claim, out var entrepriseId))
            {
                httpContext.Items[EntrepriseCacheKey] = entrepriseId;
                return entrepriseId;
            }

            var userId = TryGetCurrentUserId(httpContext.User);
            if (!userId.HasValue)
            {
                return null;
            }

            var resolvedEntrepriseId = dbContext.Utilisateur
                .AsNoTracking()
                .Where(u => u.Id == userId.Value)
                .Select(u => (int?)u.IdEntreprise)
                .FirstOrDefault();

            if (resolvedEntrepriseId.HasValue)
            {
                httpContext.Items[EntrepriseCacheKey] = resolvedEntrepriseId.Value;
            }

            return resolvedEntrepriseId;
        }

        public static bool CanCurrentUserAccessMetadonnee(HttpContext httpContext, ApplicationDbContext dbContext, Metadonnee metadonnee, out bool requiresAuthentication)
        {
            var isAuthenticated = httpContext.User.Identity?.IsAuthenticated ?? false;
            requiresAuthentication = false;

            if (metadonnee.IdVisibilite == VisibiliteIds.Public)
            {
                return true;
            }

            if (!isAuthenticated)
            {
                requiresAuthentication = true;
                return false;
            }

            var role = GetCurrentUserRole(httpContext, dbContext);
            var userId = TryGetCurrentUserId(httpContext.User);

            switch (metadonnee.IdVisibilite)
            {
                case VisibiliteIds.Prive:
                    return true;

                case VisibiliteIds.Interne:
                    if (role == RoleIds.Administrateur)
                    {
                        return true;
                    }

                    if (role == RoleIds.Utilisateur || role == RoleIds.Editeur)
                    {
                        var entrepriseId = GetCurrentUserEntrepriseId(httpContext, dbContext);
                        return entrepriseId.HasValue &&
                               metadonnee.Utilisateur != null &&
                               metadonnee.Utilisateur.IdEntreprise == entrepriseId.Value;
                    }

                    return false;

                case VisibiliteIds.Personnelle:
                    if (role == RoleIds.Administrateur)
                    {
                        return true;
                    }

                    return userId.HasValue && metadonnee.IdUtilisateur == userId.Value;

                default:
                    return false;
            }
        }
    }
}
