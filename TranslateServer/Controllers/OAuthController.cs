using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TranslateServer.Mcp;

namespace TranslateServer.Controllers
{
    /// <summary>
    /// Minimal built-in OAuth 2.0 Authorization Server endpoints.
    /// Supports Authorization Code Flow + PKCE for MCP clients (Grok, etc.).
    /// </summary>
    [Route("api/oauth")]
    public class OAuthController : Controller
    {
        private readonly OAuthServerService _oauthService;
        private readonly McpOptions _mcpOptions;

        public OAuthController(OAuthServerService oauthService, Microsoft.Extensions.Options.IOptions<McpOptions> mcpOptions)
        {
            _oauthService = oauthService;
            _mcpOptions = mcpOptions.Value;
        }

        /// <summary>
        /// Authorization Endpoint - Step 1
        /// </summary>
        [HttpGet("authorize")]
        public async Task<IActionResult> Authorize(
            [FromQuery] string response_type,
            [FromQuery] string client_id,
            [FromQuery] string redirect_uri,
            [FromQuery] string? scope,
            [FromQuery] string? state,
            [FromQuery] string? code_challenge,
            [FromQuery] string? code_challenge_method)
        {
            var client = _oauthService.GetClient(client_id);
            if (client == null)
                return BadRequest("Unknown client");

            if (!_oauthService.ValidateRedirectUri(client, redirect_uri))
                return BadRequest("Invalid redirect_uri");

            if (response_type != "code")
                return BadRequest("Only response_type=code is supported");

            // If user is not logged in via the main application, redirect to login
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                // For simplicity we redirect to the main login page
                var returnUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;
                return Redirect($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            // Show consent screen
            ViewBag.ClientName = client.Name;
            ViewBag.Scope = scope ?? "mcp:access";
            ViewBag.ClientId = client_id;
            ViewBag.RedirectUri = redirect_uri;
            ViewBag.State = state;
            ViewBag.CodeChallenge = code_challenge;
            ViewBag.CodeChallengeMethod = code_challenge_method;

            return View("Consent");
        }

        /// <summary>
        /// Consent confirmation - Step 2
        /// </summary>
        [HttpPost("authorize")]
        [Authorize]
        public async Task<IActionResult> ConsentConfirmed(
            string client_id,
            string redirect_uri,
            string? state,
            string? code_challenge,
            string? code_challenge_method,
            string action)
        {
            var client = _oauthService.GetClient(client_id);
            if (client == null || !_oauthService.ValidateRedirectUri(client, redirect_uri))
                return BadRequest();

            if (action == "deny")
            {
                var errorRedirect = $"{redirect_uri}?error=access_denied";
                if (!string.IsNullOrEmpty(state)) errorRedirect += $"&state={Uri.EscapeDataString(state)}";
                return Redirect(errorRedirect);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "unknown";
            var agentName = User.Identity?.Name ?? "MCP-User";

            // Create authorization code
            var code = _oauthService.CreateAuthorizationCode(
                client_id,
                redirect_uri,
                code_challenge,
                code_challenge_method,
                userId,
                agentName
            );

            var successRedirect = $"{redirect_uri}?code={Uri.EscapeDataString(code)}";
            if (!string.IsNullOrEmpty(state))
                successRedirect += $"&state={Uri.EscapeDataString(state)}";

            return Redirect(successRedirect);
        }

        /// <summary>
        /// Token Endpoint
        /// </summary>
        [HttpPost("token")]
        public async Task<IActionResult> Token(
            [FromForm] string grant_type,
            [FromForm] string code,
            [FromForm] string redirect_uri,
            [FromForm] string client_id,
            [FromForm] string? client_secret,
            [FromForm] string? code_verifier)
        {
            if (grant_type != "authorization_code")
                return BadRequest(new { error = "unsupported_grant_type" });

            var client = _oauthService.GetClient(client_id);
            if (client == null)
                return BadRequest(new { error = "invalid_client" });

            // For confidential clients, validate secret (optional for PKCE public clients)
            if (!string.IsNullOrEmpty(client.ClientSecret) && client.ClientSecret != client_secret)
                return BadRequest(new { error = "invalid_client" });

            var authCode = _oauthService.ConsumeAuthorizationCode(code, redirect_uri, code_verifier);
            if (authCode == null)
                return BadRequest(new { error = "invalid_grant" });

            var accessToken = _oauthService.IssueAccessToken(authCode);

            return Ok(new
            {
                access_token = accessToken,
                token_type = "Bearer",
                expires_in = 3600,
                scope = "mcp:access"
            });
        }
    }
}
