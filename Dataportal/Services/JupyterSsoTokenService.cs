using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Linq;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace Dataportal.Services
{
    /// <summary>
    /// Generates DataPortal-issued JWTs for the internal JupyterHub SSO integration.
    /// </summary>
    public class JupyterSsoTokenService
    {
        public const int TokenLifetimeMinutes = 15;
        private const string JupyterAudience = "jupyter";
        private const string JupyterScope = "notebooks.read notebooks.update";

        private readonly JupyterSsoOptions _options;

        public JupyterSsoTokenService(IOptions<JupyterSsoOptions> options)
        {
            _options = options.Value;
        }

        public JupyterSsoTokenResult CreateToken(ClaimsPrincipal principal)
        {
            var subject = principal.FindFirst("UserId")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? principal.Identity?.Name;

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new InvalidOperationException("Unable to determine subject for Jupyter SSO token.");
            }

            var displayName = principal.FindFirst("NomComplet")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value;

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim("scope", JupyterScope)
            };

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Name, displayName));
            }

            foreach (var role in principal.FindAll(ClaimTypes.Role).Select(r => r.Value).Distinct())
            {
                claims.Add(new Claim("roles", role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(TokenLifetimeMinutes);

            var token = new JwtSecurityToken(
                audience: JupyterAudience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials);

            var handler = new JwtSecurityTokenHandler();
            return new JupyterSsoTokenResult(handler.WriteToken(token), (int)TimeSpan.FromMinutes(TokenLifetimeMinutes).TotalSeconds);
        }
    }

    public sealed record JupyterSsoTokenResult(string AccessToken, int ExpiresInSeconds);
}