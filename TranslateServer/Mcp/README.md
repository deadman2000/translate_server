# MCP / AI Agent Integration (Internal)

This folder contains the MCP (Model Context Protocol) / LLM agent surface for TranslateServer.

## Two Surfaces

TranslateServer now exposes **two** interfaces for AI agents:

1. **REST API** (legacy / convenient for direct calls)
   - `POST /api/mcp/*`
   - Protected by `X-MCP-Token` or `Authorization: Bearer`

2. **Real MCP Protocol Server** (recommended for modern LLM clients)
   - `POST /mcp` (SSE / Streamable HTTP transport)
   - This is a **real** MCP server using the official `ModelContextProtocol` SDK.
   - LLM clients (LM Studio, Claude Desktop, Cursor, Continue.dev, etc.) can connect natively.

## Configuration (Multiple Agents)

The system supports **multiple AI agents**, each with its own token and attribution name.

### Configuration

```json
"Mcp": {
  "Enabled": true,
  "Agents": [
    {
      "Token": "claude-secret-xxx",
      "AgentName": "Claude-3.5-Sonnet",
      "Description": "Main Claude agent"
    },
    {
      "Token": "grok-secret-yyy",
      "AgentName": "Grok-4",
      "Description": "Grok agent for heavy reasoning tasks"
    },
    {
      "Token": "qwen-local-token",
      "AgentName": "Qwen2.5-32B-Local",
      "Description": "Local Qwen model"
    },
    {
      "Token": "readonly-audit-token-999",
      "AgentName": "Audit-Bot",
      "Description": "Read-only agent for analysis and searching",
      "ReadOnly": true
    }
  ]
}
```

The `Agents` array is required. Each entry must have at least `Token` and `AgentName`.

The same token mechanism protects both `/api/mcp` (REST) and `/mcp` (real MCP protocol).

### Read-only agents

You can mark an agent as read-only:

```json
{
  "Token": "readonly-token-abc",
  "AgentName": "Audit-Bot",
  "ReadOnly": true
}
```

Such agents can use:
- `list_projects`, `list_volumes`, `get_text_context`, `list_texts`, `search`, `get_translation_history`, `get_status`

They **cannot** use:
- `propose_translation`
- `add_comment`

This is useful for analysis bots, auditors, or agents you want to restrict from modifying translations.

## Connecting Real MCP Clients

### LM Studio
1. Go to **Program** → **MCP Servers** (or Agent settings).
2. Add a new MCP server pointing to:
   ```
   http://localhost:5000/mcp
   ```
3. Configure the header `X-MCP-Token: <your-token>` (LM Studio supports custom headers for MCP).

### Claude Desktop / Other clients
For stdio-based clients you will need a small wrapper (we can add one later). HTTP transport works directly with many modern clients.

### Cursor / Continue.dev
Most tools that support remote MCP servers can point to `http://your-server/mcp` and pass the auth header.

## Available MCP Tools

| Tool                        | Description                                                                 |
|----------------------------|-----------------------------------------------------------------------------|
| `get_status`               | Server info + agent name                                                    |
| `list_projects`            | All projects with progress stats                                            |
| `list_volumes`             | Volumes inside a project                                                    |
| `get_text_context`         | **Most powerful tool** — line + surrounding context + current translation + comments |
| `list_texts`               | Paginated list with filters (untranslated / unapproved / etc.)              |
| `search`                   | Full-text search in source + translations (critical for consistency)        |
| `propose_translation`      | Submit a new AI translation (with optional reason + confidence)             |
| `add_comment`              | Leave a review comment on any translation                                   |
| `get_translation_history`  | Full edit history for a line                                                |

## Best Practices for the LLM

- Always call `get_text_context` before proposing a change.
- Use `search` aggressively to keep terminology consistent.
- Always provide a good `reason` when calling `propose_translation`.
- Use `add_comment` to critique existing translations.

## Security Notes

- Two authentication methods are supported:
  1. **Static tokens** (`X-MCP-Token` or simple `Bearer`) — defined in `Mcp:Agents[]`
  2. **JWT Bearer** — for OAuth clients (Grok, Claude with OAuth, etc.)

- Enable JWT support in `Mcp:Jwt` section when you want to allow connections from Grok.

- AI proposals are attributed using either the static `AgentName` or claims from the JWT (`name`, `sub`, etc.).

- By design, the AI agent **cannot approve** translations or delete other users' work.

## Architecture

- Business logic lives in `McpAgentService`.
- REST surface = `McpController` (thin).
- Real MCP tools = `TranslateMcpTools` (uses the same service).
- Authentication for both surfaces is handled via the `Agents[].Token` values.

### Important: Reverse Proxy

If your backend is behind a reverse proxy that only forwards `/api/*` and `/mcp/*`, make sure to also forward:

- `/api/oauth/*` (for the built-in OAuth server)
- `/api/.well-known/oauth-protected-resource` (OAuth metadata discovery)

The root `/.well-known/oauth-protected-resource` may not be reachable in such setups.

## Next Possible Improvements

- Dedicated stdio MCP host (for Claude Desktop, etc.).
- Store `reason`/`confidence` directly on `TextTranslate`.
- Add glossary / terminology tools.
- Support for multiple AI agents with different permission levels.
