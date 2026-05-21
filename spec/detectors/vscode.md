# Detector: VS Code (+ AI extensions)

## App detection

| Platform | Path |
|----------|------|
| macOS | `/Applications/Visual Studio Code.app` or `~/Applications/Visual Studio Code.app` |
| Windows | `%LOCALAPPDATA%\Programs\Microsoft VS Code\` (directory existence) |

## Extensions directory

| Platform | Path |
|----------|------|
| macOS | `~/.vscode/extensions/` |
| Windows | `%USERPROFILE%\.vscode\extensions\` |

## Detected AI extension patterns

Scan the extensions directory for folder names matching these prefixes:

| Extension ID prefix | Name | AES-EXT-001 |
|--------------------|------|-------------|
| `saoudrizwan.claude-dev-` | Cline | Yes |
| `rooveterinaryinc.roo-cline-` | Roo | Yes |
| `continue.continue-` | Continue | Yes |
| `github.copilot-` | GitHub Copilot | No (informational only) |

Extensions matching the first 3 patterns trigger AES-EXT-001.

## Per-extension config files

### Continue
`~/.continue/config.json` (both platforms — in home root, not OS-specific)

### Cline
- macOS: `~/Library/Application Support/Code/User/globalStorage/saoudrizwan.claude-dev/`
- Windows: `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\`

Scan this directory for `mcp_settings.json` or similar. Parse for MCP server entries if found.

### Roo
Same pattern as Cline, substitute extension ID `rooveterinaryinc.roo-cline`.

## Extracted facts

`ExtensionFact`:

| Field | Value |
|-------|-------|
| `extensionId` | full extension ID including version |
| `extensionName` | display name |
| `appId` | `"vscode"` |
| `hasTerminalAccess` | `true` for Cline/Roo/Continue |

Plus `McpServerFact` for any MCP servers found in extension config files.

## Rules applied

AES-EXT-001, AES-MCP-001..007, AES-AUTH-001, AES-AUTH-002, AES-CFG-003
