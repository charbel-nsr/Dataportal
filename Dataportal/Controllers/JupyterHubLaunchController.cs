using Dataportal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dataportal.Controllers
{
    /// <summary>
    /// Launches JupyterHub with a short-lived SSO token.
    /// </summary>
    [Authorize(Roles = "administrator,editor")]
    [Route("jupyter")]
    public class JupyterHubLaunchController : Controller
    {
        private readonly JupyterSsoTokenService _tokenService;

        public JupyterHubLaunchController(JupyterSsoTokenService tokenService)
        {
            _tokenService = tokenService;
        }

        /// <summary>
        /// Redirects authorized users to JupyterHub with a short-lived access token.
        /// </summary>
        [HttpGet("launch")]
        public IActionResult Launch()
        {
            var tokenResult = _tokenService.CreateToken(User);
            var escapedToken = Uri.EscapeDataString(tokenResult.AccessToken);
            var redirectUrl = $"/jupyterhub/hub/login?token={escapedToken}&next=%2Fjupyterhub%2Fhub%2Fuser-redirect%2Flab";

            return Redirect(redirectUrl);
        }
    }
}