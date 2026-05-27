namespace TranslateServer.Mcp
{
    /// <summary>
    /// Configuration for MCP / AI agent access.
    /// </summary>
    public class McpOptions
    {
        /// <summary>
        /// Master token for MCP access. All MCP requests must provide this
        /// via header "X-MCP-Token" or "Authorization: Bearer &lt;token&gt;".
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Name that will be used as Author/Editor for all translations
        /// and comments created through MCP (e.g. "Claude", "Grok", "AI-Assistant").
        /// </summary>
        public string AgentName { get; set; } = "AI";

        /// <summary>
        /// Whether MCP endpoints are enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
