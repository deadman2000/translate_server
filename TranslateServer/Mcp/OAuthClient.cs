using System.Collections.Generic;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// Configuration for an OAuth client that can request access to this MCP server.
    /// Used for built-in OAuth flow (for Grok, Claude, etc.).
    /// </summary>
    public class OAuthClient
    {
        /// <summary>
        /// Unique client identifier.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// Client secret (for confidential clients). Can be empty for public clients using PKCE.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// List of allowed redirect URIs.
        /// </summary>
        public List<string> RedirectUris { get; set; } = new();

        /// <summary>
        /// Human-readable name of the client (shown on consent screen).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Whether this client is allowed to request tokens.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default scopes this client can request.
        /// </summary>
        public List<string> AllowedScopes { get; set; } = new() { "mcp:access" };
    }
}
