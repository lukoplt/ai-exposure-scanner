# Detector: Windsurf

## App detection

| Platform | Path |
|----------|------|
| macOS | `/Applications/Windsurf.app` or `~/Applications/Windsurf.app` |
| Windows | `%LOCALAPPDATA%\Programs\Windsurf\` (directory existence) |

## Config files

| Platform | MCP config | Settings |
|----------|-----------|----------|
| macOS | `~/.codeium/windsurf/mcp_config.json` | `~/Library/Application Support/Windsurf/User/settings.json` |
| Windows | `%USERPROFILE%\.codeium\windsurf\mcp_config.json` | `%APPDATA%\Windsurf\User\settings.json` |

## MCP config format

```json
{
  "mcpServers": {
    "my-server": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/Users/alice"]
    }
  }
}
```

## Extracted facts

`McpServerFact` — same fields as Claude Desktop with `appId = "windsurf"`.

## Rules applied

AES-MCP-001..007, AES-AUTH-001, AES-AUTH-002, AES-CFG-003, AES-CFG-004
