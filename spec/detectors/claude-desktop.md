# Detector: Claude Desktop

## App detection

| Platform | Path |
|----------|------|
| macOS | `/Applications/Claude.app` or `~/Applications/Claude.app` |
| Windows | `%LOCALAPPDATA%\AnthropicClaude\` (directory existence) |

## Config file

| Platform | Path |
|----------|------|
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |

## Config format

JSON. Top-level key `mcpServers` is a map from server name to server config.

```json
{
  "mcpServers": {
    "filesystem": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/Users/alice"],
      "env": {
        "SOME_KEY": "value"
      }
    },
    "disabled-server": {
      "command": "npx",
      "args": ["-y", "some-server"],
      "disabled": true
    }
  }
}
```

## Extracted facts (per MCP server entry)

Type: `McpServerFact`

| Field | Source | Type |
|-------|--------|------|
| `name` | map key | String |
| `command` | `command` field | String |
| `args` | `args` array | [String] |
| `env` | `env` map values | [String: String] |
| `disabled` | `disabled` field | Bool (default false if absent) |
| `appId` | constant `"claude-desktop"` | String |
| `configPath` | path of config file | String |

## Rules applied

AES-MCP-001, AES-MCP-002, AES-MCP-003, AES-MCP-004, AES-MCP-005,
AES-MCP-006, AES-MCP-007, AES-AUTH-001, AES-AUTH-002, AES-CFG-003, AES-CFG-004

## Notes

- If `mcpServers` key is absent or empty, no MCP findings are generated (app may still be detected).
- `disabled: true` entries still trigger AES-MCP-006.
- Config file max read size: 10 MB. Files larger than this are skipped with a warning finding.
