using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using TranslateServer.Mcp;

namespace TranslateServer.Controllers
{
    /// <summary>
    /// Minimal built-in OAuth 2.0 Authorization Server endpoints.
    /// Supports Authorization Code Flow + PKCE for MCP clients (Grok, etc.).
    /// </summary>
    [Route("api/oauth")]
    public class OAuthController : ControllerBase
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

            // If user is not logged in via the main application, we cannot show consent.
            // In a reverse-proxy setup where only /api/* and /mcp/* are exposed,
            // the main login page is usually not reachable.
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Content("<html><body><h2>Authentication required</h2>" +
                               "<p>Please log in through the main application first, then try the OAuth flow again.</p>" +
                               "</body></html>", "text/html");
            }

            // Return a self-contained consent page (no Razor views / TempData required)
            string html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Authorization Request</title>
    <style>
        body {{ font-family: system-ui, -apple-system, sans-serif; max-width: 480px; margin: 60px auto; padding: 20px; line-height: 1.5; }}
        .card {{ border: 1px solid #ddd; border-radius: 8px; padding: 24px; box-shadow: 0 2px 8px rgba(0,0,0,0.05); }}
        .buttons {{ margin-top: 24px; display: flex; gap: 12px; }}
        button {{ padding: 10px 24px; font-size: 15px; border-radius: 6px; cursor: pointer; border: none; }}
        .allow {{ background: #0d6efd; color: white; }}
        .deny {{ background: #6c757d; color: white; }}
        code {{ background: #f4f4f4; padding: 2px 6px; border-radius: 4px; }}
    </style>
</head>
<body>
    <div class='card'>
        <h2>Authorization Request</h2>
        <p><strong>{client.Name}</strong> wants to connect to your MCP server.</p>
        <p>Requested scope: <code>{HttpUtility.HtmlEncode(scope ?? "mcp:access")}</code></p>
        
        <form method='post' action='/api/oauth/authorize'>
            <input type='hidden' name='client_id' value='{HttpUtility.HtmlEncode(client_id)}' />
            <input type='hidden' name='redirect_uri' value='{HttpUtility.HtmlEncode(redirect_uri)}' />
            <input type='hidden' name='state' value='{HttpUtility.HtmlEncode(state)}' />
            <input type='hidden' name='code_challenge' value='{HttpUtility.HtmlEncode(code_challenge)}' />
            <input type='hidden' name='code_challenge_method' value='{HttpUtility.HtmlEncode(code_challenge_method)}' />

            <div class='buttons'>
                <button type='submit' name='action' value='allow' class='allow'>Allow access</button>
                <button type='submit' name='action' value='deny' class='deny'>Deny</button>
            </div>
        </form>
    </div>
</body>
</html>";

            return Content(html, "text/html");
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
