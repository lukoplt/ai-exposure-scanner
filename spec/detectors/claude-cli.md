# Claude CLI Detector

**App ID:** `claude-cli`  
**Display name:** Claude CLI

## Config paths

| Platform | Path |
|----------|------|
| macOS / Linux | `~/.claude.json` |
| Windows | `%USERPROFILE%\.claude.json` |

## Installation detection

Presence of `~/.claude/` directory (`%USERPROFILE%\.claude\` on Windows).

## Facts collected

### MCP servers (`mcpServers` key in `~/.claude.json`)

Same JSON format as Claude Desktop `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "server-name": {
      "command": "string",
      "args": ["..."],
      "env": { "KEY": "value" }
    }
  }
}
```

Parsed by the shared `parseMcpServersJson` / `ParseMcpServersJson` helper.  
All MCP rules (AES-MCP-001 … AES-MCP-007) apply.  
Credentials in `env` values trigger AES-AUTH-001.

### Config file fact

Appended when `~/.claude.json` exists, enabling AES-CFG-003 (server with no description).

## Rules that apply

AES-MCP-001, AES-MCP-002, AES-MCP-003, AES-MCP-004, AES-MCP-005, AES-MCP-006, AES-MCP-007, AES-AUTH-001
