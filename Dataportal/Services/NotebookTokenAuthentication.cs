using Dataportal.Classes;
using Dataportal.Context;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Dataportal.Services
{
    public static class NotebookTokenDefaults
    {
        public const string AuthenticationScheme = "NotebookToken";
    }

    public class NotebookTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private const string NotebookTokenHeader = "X-Notebook-Token";
        private const string ApiKeyHeader = "X-Api-Key";
        private const string BearerPrefix = "Bearer ";
        private readonly ApplicationDbContext _context;

        public NotebookTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            ApplicationDbContext context)
            : base(options, logger, encoder, clock)
        {
            _context = context;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = ExtractToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                return AuthenticateResult.NoResult();
            }

            var tokenHash = SecurityTokenHelper.ComputeSha256(token);
            var notebookToken = await _context.NotebookApiTokens
                .Include(t => t.Utilisateur)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, Context.RequestAborted);

            if (notebookToken == null || notebookToken.RevokedAtUtc.HasValue)
            {
                return AuthenticateResult.Fail("Invalid token.");
            }

            var user = notebookToken.Utilisateur;
            if (user == null)
            {
                return AuthenticateResult.Fail("Token user not found.");
            }

            var claims = new List<Claim>
            {
                new("UserId", user.Id.ToString(CultureInfo.InvariantCulture)),
                new("RoleId", user.IdRole.ToString(CultureInfo.InvariantCulture)),
                new("EntrepriseId", user.IdEntreprise.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
                new(ClaimTypes.Name, $"{user.Prenom} {user.Nom}".Trim())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            notebookToken.LastUsedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(Context.RequestAborted);

            return AuthenticateResult.Success(ticket);
        }

        private string? ExtractToken()
        {
            if (Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorization))
            {
                var headerValue = authorization.ToString();
                if (headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var bearerToken = headerValue[BearerPrefix.Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(bearerToken))
                    {
                        return bearerToken;
                    }
                }
            }

            if (Request.Headers.TryGetValue(NotebookTokenHeader, out var tokenHeader))
            {
                var tokenValue = tokenHeader.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(tokenValue))
                {
                    return tokenValue;
                }
            }

            if (Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyHeader))
            {
                var apiKeyValue = apiKeyHeader.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(apiKeyValue))
                {
                    return apiKeyValue;
                }
            }

            return null;
        }
    }
}