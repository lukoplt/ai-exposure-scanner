# Detector: Gemini CLI

## Tool detection

| Platform | Path |
|----------|------|
| Both | `~/.gemini/` directory existence |

## Config files

| Platform | Path |
|----------|------|
| Both | `~/.gemini/settings.json` |
| Both | `~/.gemini/credentials` or `~/.gemini/oauth_credentials.json` (existence check only) |

## Config format (settings.json)

JSON. MCP servers under `mcpServers` key (same schema as Claude Desktop):

```json
{
  "mcpServers": {
    "my-server": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-filesystem", "/Users/alice"]
    }
  },
  "apiKey": "AIzaSy-FAKEFAKEFAKEFAKE"
}
```

Note: `apiKey` at top level in settings.json also triggers AES-AUTH-001 (treat as if it were in env map).

## Extracted facts

`McpServerFact` with `appId = "gemini-cli"` for each MCP server.

`AuthFileFact` when credentials file exists (existence check only):

| Field | Value |
|-------|-------|
| `filePath` | path to credentials file |
| `appId` | `"gemini-cli"` |

`SettingFact` for `apiKey` in settings.json (value masked in findings).

## Rules applied

AES-MCP-001..007, AES-AUTH-001, AES-AUTH-002, AES-AUTH-003, AES-CFG-003, AES-CFG-004
