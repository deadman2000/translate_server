using System;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// Holds information about the currently authenticated MCP agent for the current request.
    /// Registered as Scoped, so it is unique per HTTP request.
    /// </summary>
    public class McpAgentContext
    {
        /// <summary>
        /// The agent that was successfully authenticated for this request.
        /// Null if no valid token was provided (should not happen after auth filter/middleware).
        /// </summary>
        public McpAgent CurrentAgent { get; private set; }

        /// <summary>
        /// Convenient shortcut to the name that should be used for authorship.
        /// </summary>
        public string AgentName => CurrentAgent?.AgentName ?? "AI";

        /// <summary>
        /// Whether the current agent is restricted to read-only operations.
        /// </summary>
        public bool IsReadOnly => CurrentAgent?.ReadOnly ?? false;

        /// <summary>
        /// Whether an agent was successfully authenticated for this request.
        /// </summary>
        public bool IsAuthenticated => CurrentAgent != null;

        /// <summary>
        /// Sets the current agent for this request.
        /// Called by the authorization middleware/filter after successful token validation.
        /// </summary>
        public void SetCurrentAgent(McpAgent agent)
        {
            CurrentAgent = agent ?? throw new ArgumentNullException(nameof(agent));
        }

        /// <summary>
        /// Clears the current agent (mainly for tests or edge cases).
        /// </summary>
        public void Clear()
        {
            CurrentAgent = null;
        }
    }
}
