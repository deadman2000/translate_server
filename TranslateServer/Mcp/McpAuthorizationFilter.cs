using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// Action filter that enforces MCP token authentication.
    /// Does NOT interfere with the main cookie-based authentication.
    /// </summary>
    public class McpAuthorizationFilter : IAsyncActionFilter
    {
        private readonly McpOptions _options;

        public McpAuthorizationFilter(IOptions<McpOptions> options)
        {
            _options = options.Value;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!_options.Enabled)
            {
                context.Result = new NotFoundResult();
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.Token))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "MCP access is not configured on the server." });
                return;
            }

            string providedToken = ExtractToken(context.HttpContext.Request);

            if (string.IsNullOrEmpty(providedToken) || !string.Equals(providedToken, _options.Token, StringComparison.Ordinal))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid or missing MCP token." });
                return;
            }

            // Token is valid. Continue to the action.
            // We do not modify the User principal to avoid any side effects on other code.
            await next();
        }

        private static string ExtractToken(Microsoft.AspNetCore.Http.HttpRequest request)
        {
            // 1. X-MCP-Token header (recommended for simplicity)
            if (request.Headers.TryGetValue("X-MCP-Token", out var headerToken))
            {
                return headerToken.ToString();
            }

            // 2. Authorization: Bearer <token>
            if (request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var value = authHeader.ToString();
                if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring("Bearer ".Length).Trim();
                }
                if (value.StartsWith("Token ", StringComparison.OrdinalIgnoreCase))
                {
                    return value.Substring("Token ".Length).Trim();
                }
            }

            // 3. Query string fallback (useful for quick testing, not recommended for production)
            if (request.Query.TryGetValue("mcp_token", out var queryToken))
            {
                return queryToken.ToString();
            }

            return null;
        }
    }
}
