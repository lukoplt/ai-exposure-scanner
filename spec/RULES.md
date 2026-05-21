# AI Exposure Scanner — Rule Catalog

This is the canonical source of truth for all detection rules. Both the macOS (Swift) and Windows (C#) implementations must implement every rule listed here with identical behavior, verified by the fixture corpus and CI drift-check.

## Rule ID schema

Format: `AES-{CATEGORY}-{NNN}`

| Prefix | Category |
|--------|----------|
| `AES-MCP-xxx` | MCP server findings |
| `AES-AUTH-xxx` | Credentials / tokens |
| `AES-EXT-xxx` | Extension / plugin findings |
| `AES-CFG-xxx` | Config hygiene |

**IDs are permanent.** Deprecated rules keep their ID and gain `status: deprecated`. IDs are never reused.

---

## Rule table

| Severity | ID | Name | Applies to |
|----------|-----|------|-----------|
| Critical | AES-MCP-001 | MCP server has broad home directory access | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| Critical | AES-MCP-002 | MCP server allows shell command execution | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| Critical | AES-AUTH-001 | API key in plain text in MCP server env | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| Critical | AES-AUTH-002 | API key in plain text in MCP server args | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| High | AES-MCP-003 | MCP server has filesystem access (broad path, below home root) | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| High | AES-MCP-004 | MCP server has network access | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| High | AES-MCP-005 | MCP server reads browser data | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| High | AES-EXT-001 | AI coding extension can access terminal | vscode |
| High | AES-CFG-001 | Cursor Privacy Mode disabled | cursor |
| Medium | AES-MCP-006 | Unused MCP server still configured | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| Medium | AES-MCP-007 | MCP server runs without pinned version | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| Medium | AES-AUTH-003 | Auth token file present in plain text | codex-cli, gemini-cli |
| Medium | AES-CFG-002 | Overlapping MCP servers across clients | cross-client |
| Low | AES-CFG-003 | MCP server has no description | claude-desktop, cursor, windsurf, vscode, codex-cli, gemini-cli |
| Low | AES-CFG-004 | AI tool config file is world-readable | claude-desktop, cursor, windsurf, codex-cli, gemini-cli |

---

## Rule details

### AES-MCP-001 — MCP server has broad home directory access

**Severity:** Critical

**Trigger:** An MCP server's `args` or `env` values contain a home directory path used as root:
- `~/` (any platform, shell expansion)
- `/Users/` (macOS, as path root — not as prefix to a specific user subdirectory)
- `C:\Users\` (Windows)
- `%USERPROFILE%` (Windows env var)
- `%HOMEPATH%` (Windows env var)

Paths like `/Users/alice/projects/myapp` do **not** trigger this rule — they are scoped. Paths like `/Users/alice` or `/Users/alice/` do trigger it.

**Why this matters:** This MCP server has access to the entire home directory. If an AI agent receives a malicious instruction from a document, website, or prompt, it may use this server to read sensitive files, credentials, or private documents without any further escalation.

**Recommendation:** Restrict this server to a specific project folder (e.g. `/Users/alice/projects/myapp`) or disable it when not actively needed.

---

### AES-MCP-002 — MCP server allows shell command execution

**Severity:** Critical

**Trigger:** An MCP server's `command` field is a shell binary, or its `args` contain shell execution flags:
- Commands: `bash`, `sh`, `zsh`, `fish`, `cmd`, `cmd.exe`, `powershell`, `powershell.exe`, `pwsh`, `pwsh.exe`
- Args containing: `--allow-run`, `--exec`, `execute`, `-c` (when command is a shell)

**Why this matters:** A shell-execution MCP server means an AI agent can run arbitrary commands on the machine. A single malicious prompt injection (from a document, webpage, or crafted message) could escalate to full command execution.

**Recommendation:** Remove or disable this MCP server. If shell execution is genuinely needed for a workflow, isolate it to a sandboxed environment and disable when not in use.

---

### AES-AUTH-001 — API key in plain text in MCP server env

**Severity:** Critical

**Trigger:** An MCP server's `env` map contains a value matching an API key pattern:

| Provider | Pattern |
|----------|---------|
| Anthropic | `sk-ant-[a-zA-Z0-9\-_]{80,}` |
| OpenAI | `sk-[a-zA-Z0-9]{48}` or `sk-proj-[a-zA-Z0-9\-_]{48,}` |
| Google AI | `AIza[a-zA-Z0-9\-_]{35}` |
| GitHub PAT | `ghp_[a-zA-Z0-9]{36}` or `github_pat_[a-zA-Z0-9_]{82}` |

The matched value is **never stored or displayed in full**. Only a masked preview is shown: first 12 characters + `••••••••••••`.

**Why this matters:** API keys in MCP server env vars are accessible to any process that reads the config file and to any tool called by the MCP server. If the config file is world-readable or synced to a shared location, the key is exposed.

**Recommendation:** Move the API key to your OS keychain (macOS Keychain, Windows Credential Manager) and reference it via the MCP server's keychain integration if supported, or use a secrets manager.

---

### AES-AUTH-002 — API key in plain text in MCP server args

**Severity:** Critical

**Trigger:** Same API key patterns as AES-AUTH-001, but matched in `args` array values rather than `env` values.

**Why this matters:** Args are often visible in process listings (`ps aux`, Task Manager) in addition to being stored in config files.

**Recommendation:** Same as AES-AUTH-001. Never pass API keys as command-line arguments.

---

### AES-MCP-003 — MCP server has filesystem access (broad path)

**Severity:** High

**Trigger:** An MCP server's `args` or `env` contains an absolute path that:
- Is not the home root (would be AES-MCP-001)
- Is not scoped to a specific project subfolder (heuristic: path depth ≤ 3 from home, or path matches known broad dirs like `~/Documents`, `~/Desktop`, `~/Downloads`)

**Why this matters:** Even without full home directory access, a broad path gives an AI agent access to many potentially sensitive files. The risk is slightly lower than AES-MCP-001 because a specific path must be targeted, but the attack surface is still significant.

**Recommendation:** Restrict to the specific project folder being worked on.

---

### AES-MCP-004 — MCP server has network access

**Severity:** High

**Trigger:** MCP server command or args reference known network-capable MCP servers:
- `@modelcontextprotocol/server-fetch`
- `mcp-server-fetch`
- `uvx mcp-server-fetch`
- `npx @modelcontextprotocol/server-fetch`
- Command is `curl`, `wget`, or similar network tool

**Why this matters:** An AI agent with network access can exfiltrate data or be instructed to fetch content from attacker-controlled URLs that contain further malicious instructions (prompt injection via web).

**Recommendation:** Disable network MCP servers when not actively needed for a specific workflow. Prefer MCP servers with explicit URL allowlists.

---

### AES-MCP-005 — MCP server reads browser data

**Severity:** High

**Trigger:** MCP server command or args reference browser automation tools:
- `playwright`, `puppeteer`, `browser-use`, `selenium`
- `@playwright/mcp`, `playwright-mcp`

**Why this matters:** Browser automation MCP servers can read session cookies, saved passwords, browsing history, and DOM content including sensitive pages. Combined with prompt injection from a malicious website, this creates a data exfiltration path.

**Recommendation:** Disable browser automation MCP servers when not actively using them for a specific task.

---

### AES-EXT-001 — AI coding extension can access terminal

**Severity:** High

**Trigger:** Any of these VS Code extension IDs are found installed:
- `saoudrizwan.claude-dev` (Cline)
- `rooveterinaryinc.roo-cline` (Roo)
- `continue.continue` (Continue)

**Why this matters:** These extensions have inherent access to the VS Code integrated terminal and can execute commands in the current workspace. Unlike MCP servers, this access is part of their core functionality and cannot be easily scoped.

**Recommendation:** Review what workspaces these extensions are active in. Avoid using them in workspaces that contain production credentials or sensitive data. Disable extensions when not in use.

---

### AES-CFG-001 — Cursor Privacy Mode disabled

**Severity:** High

**Trigger:** Cursor's `settings.json` contains `"cursor.privacyMode": false`, or the key is absent (default was `false` in Cursor versions before 0.42).

**Why this matters:** With Privacy Mode disabled, code from the current workspace is sent to Cursor's servers for AI processing. This may violate data handling policies for proprietary or sensitive codebases.

**Recommendation:** Enable Privacy Mode in Cursor Settings → Features → Privacy Mode. This prevents code from being sent to Cursor's servers.

---

### AES-MCP-006 — Unused MCP server still configured

**Severity:** Medium

**Trigger:** An MCP server entry has `"disabled": true`, or the parent AI client application is not installed on the system.

**Why this matters:** Disabled or orphaned configurations are often forgotten. If they contain credentials or broad paths, they remain a risk even if not currently active — particularly if the configuration is reused or the app is reinstalled.

**Recommendation:** Remove MCP server entries that are no longer needed. Clean configurations reduce attack surface.

---

### AES-MCP-007 — MCP server runs without pinned version

**Severity:** Medium

**Trigger:** MCP server command is `npx`, `uvx`, or `pipx` without a pinned exact version (`@x.y.z`):
- `npx @modelcontextprotocol/server-filesystem` (no version) → triggers
- `npx @modelcontextprotocol/server-filesystem@1.2.3` → does not trigger

**Why this matters:** Without a pinned version, the next run may download a different (potentially compromised) version of the MCP server. Supply chain attacks on npm/pypi packages are a known vector.

**Recommendation:** Pin exact versions: `npx @modelcontextprotocol/server-filesystem@1.2.3`.

---

### AES-AUTH-003 — Auth token file present in plain text

**Severity:** Medium

**Trigger:** Any of these files exist (existence check only, contents are not read):
- `~/.codex/auth.json`
- `~/.gemini/credentials`
- `~/.gemini/oauth_credentials.json`

**Why this matters:** Auth token files in the home directory may contain long-lived tokens. If the home directory is backed up, synced, or accessed by an MCP server with broad filesystem access, these tokens are exposed.

**Recommendation:** Review whether these tokens are still needed. If so, ensure the parent directory is not accessible to MCP servers with broad filesystem access.

---

### AES-CFG-002 — Overlapping MCP servers across clients

**Severity:** Medium

**Trigger:** Two different AI clients have MCP server configurations with the same `name` key.

**Why this matters:** Duplicate MCP server registrations across clients create redundant access paths. If one client is compromised via prompt injection, the same MCP server capability is also available through other clients.

**Recommendation:** Consolidate MCP server configuration or use different server names per client to make access paths explicit.

---

### AES-CFG-003 — MCP server has no description

**Severity:** Low

**Trigger:** An MCP server entry does not contain a `description` field (or equivalent comment).

**Why this matters:** Undescribed MCP servers make it harder to audit what each server does and whether it is still needed. This is a hygiene issue that becomes important during security reviews.

**Recommendation:** Add a `description` field to each MCP server explaining its purpose and expected access scope.

---

### AES-CFG-004 — AI tool config file is world-readable

**Severity:** Low

**Trigger:**
- macOS: `stat` shows `o+r` (other-read) permission on a config file containing MCP server configuration
- Windows: ACL includes read access for `Everyone` or `Authenticated Users` beyond what's needed

**Why this matters:** Config files may contain API keys, MCP server paths, or other sensitive configuration. World-readable permissions mean any process or user on the system can read them.

**Recommendation:** Restrict config file permissions: `chmod 600 ~/.cursor/mcp.json` (macOS/Linux) or set restrictive ACLs on Windows.

---

## API key regex patterns

Used by AES-AUTH-001 and AES-AUTH-002. Matching is structural (prefix + length + charset). Values are never stored, logged, or displayed in full.

```
Anthropic API key:  sk-ant-[a-zA-Z0-9\-_]{80,}
OpenAI API key:     sk-[a-zA-Z0-9]{48}
OpenAI Project key: sk-proj-[a-zA-Z0-9\-_]{48,}
Google AI API key:  AIza[a-zA-Z0-9\-_]{35}
GitHub PAT (v1):    ghp_[a-zA-Z0-9]{36}
GitHub PAT (v2):    github_pat_[a-zA-Z0-9_]{82}
```

Masking format: first 12 characters of matched value + `••••••••••••`
Example: `sk-ant-api03-aBcD••••••••••••`
