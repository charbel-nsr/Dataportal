using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Dataportal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dataportal.Controllers.JupyterHubSso
{
    /// <summary>
    /// Issues JWTs for internal JupyterHub SSO (not the external NotebookApi integration).
    /// </summary>
    [ApiController]
    [Route("api/jupyter-sso")]
    public class JupyterSsoController : ControllerBase
    {
        private readonly JupyterSsoTokenService _tokenService;

        public JupyterSsoController(JupyterSsoTokenService tokenService)
        {
            _tokenService = tokenService;
        }

        /// <summary>
        /// Creates a short-lived access token for internal JupyterHub SSO.
        /// </summary>
        [HttpPost("token")]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult CreateToken()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var tokenResult = _tokenService.CreateToken(User);
            return Ok(new JupyterSsoTokenResponse(tokenResult.AccessToken, tokenResult.ExpiresInSeconds));
        }

        /// <summary>
        /// Refreshes a short-lived access token for internal JupyterHub SSO.
        /// </summary>
        [HttpPost("refresh")]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult RefreshToken()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var tokenResult = _tokenService.CreateToken(User);
            return Ok(new JupyterSsoTokenResponse(tokenResult.AccessToken, tokenResult.ExpiresInSeconds));
        }

        /// <summary>
        /// Returns the current user's identity details to assist with internal JupyterHub SSO testing.
        /// </summary>
        [HttpGet("whoami")]
        [Authorize(Roles = "administrator,editor")]
        public IActionResult WhoAmI()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            var userId = User.FindFirst("UserId")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.Identity?.Name;

            var displayName = User.FindFirst("NomComplet")?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value;

            var roles = User.FindAll(ClaimTypes.Role).Select(role => role.Value).Distinct().ToList();

            return Ok(new JupyterSsoWhoAmIResponse(userId, displayName, roles));
        }

        private sealed record JupyterSsoTokenResponse(string AccessToken, int ExpiresInSeconds);

        private sealed record JupyterSsoWhoAmIResponse(string? UserId, string? DisplayName, List<string> Roles);
    }
}