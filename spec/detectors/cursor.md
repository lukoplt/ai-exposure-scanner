# Detector: Cursor

## App detection

| Platform | Path |
|----------|------|
| macOS | `/Applications/Cursor.app` or `~/Applications/Cursor.app` |
| Windows | `%LOCALAPPDATA%\Programs\cursor\` (directory existence) |

## Config files

| Platform | MCP config | Settings |
|----------|-----------|----------|
| macOS | `~/.cursor/mcp.json` | `~/Library/Application Support/Cursor/User/settings.json` |
| Windows | `%USERPROFILE%\.cursor\mcp.json` | `%APPDATA%\Cursor\User\settings.json` |

## MCP config format

Same schema as Claude Desktop `mcpServers` map:

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

## Settings format (relevant fields)

```json
{
  "cursor.privacyMode": true
}
```

If `cursor.privacyMode` is `false` or absent → triggers AES-CFG-001.

## Extracted facts

Same `McpServerFact` as Claude Desktop, plus one `SettingFact`:

| Field | Value |
|-------|-------|
| `key` | `"cursor.privacyMode"` |
| `value` | `true` / `false` / `null` (if absent) |
| `appId` | `"cursor"` |
| `configPath` | path to settings.json |

## Rules applied

AES-MCP-001..007, AES-AUTH-001, AES-AUTH-002, AES-CFG-001, AES-CFG-003, AES-CFG-004
