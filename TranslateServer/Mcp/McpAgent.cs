namespace TranslateServer.Mcp
{
    /// <summary>
    /// Configuration for a single AI agent that can access the MCP interface.
    /// Each agent has its own token and name used for attribution.
    /// </summary>
    public class McpAgent
    {
        /// <summary>
        /// Secret token for this agent. Must be unique.
        /// The agent must send this via X-MCP-Token header or Authorization: Bearer.
        /// </summary>
        public string Token { get; set; }

        /// <summary>
        /// Name that will be recorded as Author/Editor when this agent
        /// creates translations or comments (e.g. "Claude-3.5", "Grok-4", "Qwen2.5-Local").
        /// </summary>
        public string AgentName { get; set; } = "AI";

        /// <summary>
        /// Optional human-readable description of this agent.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Whether this specific agent is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// If true, this agent can only read data (projects, volumes, context, search, history).
        /// Write operations (propose_translation, add_comment) will be rejected.
        /// </summary>
        public bool ReadOnly { get; set; } = false;
    }
}
