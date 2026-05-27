using System.Collections.Generic;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// JWT / OAuth configuration for MCP access.
    /// Used when connecting via Grok, Claude (with OAuth), or other OAuth-capable MCP clients.
    /// </summary>
    public class McpJwtOptions
    {
        /// <summary>
        /// Enable JWT Bearer authentication for MCP.
        /// When enabled, valid JWTs in Authorization: Bearer header will be accepted.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Expected issuer of the JWT (e.g. https://api.x.ai or your Auth0 tenant).
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// Expected audience (usually the MCP server URL or a client id).
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// Authority URL for automatic JWKS discovery (recommended for real OAuth).
        /// Example: "https://api.x.ai" or "https://your-auth0-tenant.auth0.com/"
        /// </summary>
        public string Authority { get; set; }

        /// <summary>
        /// Optional static signing key (base64) for development / testing.
        /// Not recommended for production OAuth flows.
        /// </summary>
        public string SigningKey { get; set; }

        /// <summary>
        /// Whether to validate the issuer.
        /// </summary>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Whether to validate the audience.
        /// </summary>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Name of the claim that should be used as the AgentName (author).
        /// Common values: "name", "preferred_username", "client_id", "sub"
        /// </summary>
        public string AgentNameClaim { get; set; } = "name";

        /// <summary>
        /// Fallback claim if the primary AgentNameClaim is missing.
        /// </summary>
        public string FallbackAgentNameClaim { get; set; } = "sub";

        /// <summary>
        /// List of external Authorization Servers that clients (like Grok) should use
        /// to obtain tokens for this MCP server.
        /// 
        /// Example:
        /// "AuthorizationServers": [
        ///   "https://your-auth0-tenant.auth0.com",
        ///   "https://api.x.ai"
        /// ]
        /// </summary>
        public List<string> AuthorizationServers { get; set; } = new();
    }
}
