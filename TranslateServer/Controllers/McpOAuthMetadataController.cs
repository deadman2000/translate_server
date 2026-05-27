using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using TranslateServer.Mcp;

namespace TranslateServer.Controllers
{
    [ApiController]
    public class McpOAuthMetadataController : ControllerBase
    {
        private readonly McpOptions _options;

        public McpOAuthMetadataController(IOptions<McpOptions> options)
        {
            _options = options.Value;
        }

        [HttpGet("/.well-known/oauth-protected-resource")]
        [HttpGet("/api/.well-known/oauth-protected-resource")]
        [AllowAnonymous]
        public IActionResult GetProtectedResourceMetadata()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var resourceUrl = $"{baseUrl}/mcp";

            var metadata = new
            {
                resource = resourceUrl,
                authorization_servers = GetAuthorizationServers(baseUrl),
                bearer_methods_supported = new[] { "header" },
                resource_documentation = $"{baseUrl}/api/mcp",

                // Helpful note for reverse proxy setups
                // If your reverse proxy only forwards /api/* and /mcp/*,
                // clients should use the following path for metadata discovery:
                // https://your-domain/api/.well-known/oauth-protected-resource
            };

            return Ok(metadata);
        }

        private List<string> GetAuthorizationServers(string baseUrl)
        {
            var servers = new List<string>();

            if (_options.Jwt?.AuthorizationServers != null && _options.Jwt.AuthorizationServers.Any())
            {
                servers.AddRange(_options.Jwt.AuthorizationServers);
            }
            else if (_options.Jwt?.Enabled == true && !string.IsNullOrEmpty(_options.Jwt.Authority))
            {
                servers.Add(_options.Jwt.Authority.TrimEnd('/'));
            }
            else
            {
                servers.Add(baseUrl);
            }

            return servers.Distinct().ToList();
        }
    }
}
