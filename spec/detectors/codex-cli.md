# Detector: Codex CLI

## Tool detection

| Platform | Path |
|----------|------|
| Both | `~/.codex/` directory existence |

No binary path check — presence of config directory is sufficient.

## Config files

| Platform | Path |
|----------|------|
| Both | `~/.codex/config.toml` |
| Both | `~/.codex/auth.json` (existence check only — contents not read) |

## Config format (config.toml)

TOML format. MCP servers under `[mcp_servers]` section:

```toml
[mcp_servers]
[mcp_servers.filesystem]
command = "npx"
args = ["-y", "@modelcontextprotocol/server-filesystem", "/Users/alice"]
```

## Extracted facts

`McpServerFact` with `appId = "codex-cli"` for each MCP server.

`AuthFileFact` when `~/.codex/auth.json` exists:

| Field | Value |
|-------|-------|
| `filePath` | `~/.codex/auth.json` |
| `appId` | `"codex-cli"` |

## Rules applied

AES-MCP-001..007, AES-AUTH-001, AES-AUTH-002, AES-AUTH-003, AES-CFG-003, AES-CFG-004

## Notes

- `auth.json` existence triggers AES-AUTH-003. The file is never opened or read.
- TOML parsing: implement minimal TOML parser sufficient for `[mcp_servers.*]` sections, or use a TOML library. Do not use a shell subprocess.
