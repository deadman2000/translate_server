using System;
using System.Collections.Generic;

namespace TranslateServer.Mcp
{
    /// <summary>
    /// Configuration for MCP / AI agent access.
    /// Supports multiple agents, each with its own token and attribution name.
    /// </summary>
    public class McpOptions
    {
        /// <summary>
        /// Whether MCP access is enabled globally.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// List of allowed AI agents.
        /// Each agent has its own token and AgentName (used for authorship).
        /// 
        /// Example:
        /// "Agents": [
        ///   { "Token": "claude-secret-xxx", "AgentName": "Claude-3.5-Sonnet", "Description": "Main Claude agent" },
        ///   { "Token": "grok-secret-yyy",   "AgentName": "Grok-4", "Description": "Grok agent" }
        /// ]
        /// </summary>
        public List<McpAgent> Agents { get; set; } = new();

        /// <summary>
        /// JWT / OAuth settings. Enable this when you want to allow connections
        /// from Grok, Claude (OAuth), or other clients that use proper OAuth/JWT.
        /// </summary>
        public McpJwtOptions Jwt { get; set; } = new();

        /// <summary>
        /// Registered OAuth 2.0 clients that are allowed to perform authorization flows
        /// against this MCP server (for Grok, Claude, Cursor, etc.).
        /// </summary>
        public List<OAuthClient> OAuthClients { get; set; } = new();

        /// <summary>
        /// Tries to find an agent by its token.
        /// Returns null if token is invalid, disabled, or MCP is globally disabled.
        /// </summary>
        public McpAgent GetAgentByToken(string token)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(token))
                return null;

            if (Agents == null || Agents.Count == 0)
                return null;

            foreach (var agent in Agents)
            {
                if (agent.Enabled &&
                    !string.IsNullOrWhiteSpace(agent.Token) &&
                    string.Equals(agent.Token, token, StringComparison.Ordinal))
                {
                    return agent;
                }
            }

            return null;
        }
    }
}
