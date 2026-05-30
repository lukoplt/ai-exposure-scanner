# Scanned problems — complete catalog

This is the human-readable summary of **every problem AI Exposure Scanner looks for**.
It mirrors the canonical, implementation-level catalog in
[`spec/RULES.md`](../spec/RULES.md) — that file is the source of truth both the macOS
(Swift) and Windows (C#) engines are verified against. This page is the friendly
overview.

**24 built-in detection rules** + **8 escalation rules** that upgrade severity when
dangerous capabilities combine. Every check runs fully locally; nothing is uploaded.

## Severity legend

| Level | Meaning |
|-------|---------|
| **Critical** | Direct path to data exfiltration or unintended command execution |
| **High** | Serious risk, one condition away from Critical |
| **Medium** | Real but limited risk; hygiene issues |
| **Low** | Informational; configuration hygiene |

---

## 1. Credentials & secrets

Secrets sitting in plain text in an AI tool's config are readable by any agent — and a
single prompt injection can exfiltrate them.

| ID | Severity | What it catches |
|----|----------|-----------------|
| AES-AUTH-001 | Critical | A provider API key (Anthropic, OpenAI, Google, GitHub) in plain text in an MCP server's `env` |
| AES-AUTH-002 | Critical | A provider API key passed on the command line (`args`) |
| AES-AUTH-004 | Critical | Long-lived **cloud credentials** in `env` (AWS / Azure / GCP / DigitalOcean access keys) |
| AES-AUTH-005 | Critical | A **database connection string with an inline username + password** (`postgres://user:pass@…`, `mysql://…`, `mongodb://…`, redis, mssql, amqp) |
| AES-AUTH-006 | High | A **plaintext secret** in `env` whose key name signals a secret (`*_TOKEN`, `*_SECRET`, `*_PASSWORD`, `*_API_KEY`…) — catches secrets the provider regexes miss |
| AES-AUTH-003 | Medium | An auth-token file present in plain text in a known AI-tool location (Codex / Gemini CLI) |
| AES-CFG-001 | High | Cursor **Privacy Mode disabled** — workspace code may be sent for AI processing |

Secret **values are never shown unmasked** — only the schema prefix survives
(`sk-ant-••••••••`, `AIza••••••••`).

## 2. MCP server permissions

What an MCP server is *allowed* to touch on your machine.

| ID | Severity | What it catches |
|----|----------|-----------------|
| AES-MCP-001 | Critical | Server has **broad home-directory access** (`~`, `/Users/<you>`, `%USERPROFILE%` as a root) |
| AES-MCP-002 | Critical | Server **allows shell command execution** (command is a shell, or shell exec flags) |
| AES-MCP-011 | Critical | Server is scoped to **SSH / cloud credential directories** (`~/.ssh`, `~/.aws`, `~/.gnupg`, `~/.kube`, `id_rsa`…) |
| AES-MCP-012 | Critical | Server can reach the **Docker daemon socket** or runs `--privileged` (host-root equivalent / container escape) |
| AES-MCP-003 | High | Server has filesystem access to a **broad path** below the home root (e.g. `~/Documents`) |

## 3. Supply chain & remote code

Where the server's code comes from, and whether it can change underneath you.

| ID | Severity | What it catches |
|----|----------|-----------------|
| AES-MCP-009 | Critical | Server **pipes a remote install script into a shell** (`curl … \| sh`, PowerShell `irm … \| iex`) — unreviewed remote code on every run |
| AES-MCP-007 | Medium | Server runs an **unpinned package** (`npx`/`uvx`/`pipx` with no exact version) — content can change between runs (rug-pull risk) |

## 4. Network & exfiltration surface

Outbound paths a prompt-injected agent could use to leak data.

| ID | Severity | What it catches |
|----|----------|-----------------|
| AES-MCP-008 | High | Server **overrides the AI model API base URL** to a non-official endpoint — routes every prompt/response through a third party |
| AES-MCP-010 | High | Server uses a **plaintext `http://` endpoint** to a non-local host (interception / tampering) |
| AES-MCP-004 | High | Server has **network access** (network tool or fetch server) |
| AES-MCP-005 | High | Server **reads browser data** / automates browser sessions |
| AES-MCP-013 | Medium | A **messaging / email server** (Slack, Discord, Telegram, Gmail, SendGrid, Twilio…) — a ready outbound exfiltration channel |
| AES-EXT-001 | High | An AI coding **extension can access the terminal** (VS Code: Cline, Roo, Continue) |

## 5. Configuration hygiene

Lower-risk issues that make audits harder or widen exposure.

| ID | Severity | What it catches |
|----|----------|-----------------|
| AES-MCP-006 | Medium | An **unused / orphaned** MCP server still configured (e.g. left behind by an uninstalled app) |
| AES-CFG-002 | Medium | The **same MCP server configured across multiple clients** (overlapping registrations) |
| AES-CFG-003 | Low | An MCP server with **no description** — undocumented purpose / scope |
| AES-CFG-004 | Low | An AI-tool **config file that is world-readable** |

---

## Escalations — dangerous combinations

Individually-rated findings are **automatically escalated to Critical** when capabilities
combine on the same server (or across enough servers) to form a real attack path:

- Broad filesystem **+** shell execution → exfiltration path
- Broad filesystem **+** network → silent data upload
- Shell execution **+** network → remote code execution
- Plaintext API key **+** network → immediate credential exfiltration
- Network access exposed across **3+ MCP servers** → broad attack surface

---

## Coverage notes & roadmap

These checks read **declared configuration** of installed AI tools. They do not yet cover
some risks that live in settings files the scanner does not parse — most notably
**auto-approve / "YOLO" tool-execution** modes (Cursor, Windsurf, IDE agents) and
**workspace-trust** toggles. Those are tracked as future rules. Org-specific checks can be
added today via [custom YAML rule packs](../README.md#custom-rule-packs) without a code
change.

To propose or implement a new rule, see [`spec/RULES.md`](../spec/RULES.md) and
`CONTRIBUTING.md`.
