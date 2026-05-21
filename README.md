# AI Exposure Scanner

> Local-only security audit for your AI developer tools. Nothing leaves your computer.

AI Exposure Scanner finds installed AI tools (Claude Desktop, Cursor, Windsurf, VS Code, Codex CLI, Gemini CLI), audits their MCP server configurations for dangerous permissions, detects exposed credentials, and surfaces privacy setting issues — with severity ratings, clear explanations, and recommended fixes.

**No cloud. No account. No telemetry. Zero network calls.**

---

## What it checks

| Tool | What's audited |
|------|----------------|
| Claude Desktop | MCP server configs, filesystem access scope, shell execution, credentials in env |
| Cursor | MCP server configs, Privacy Mode setting |
| Windsurf | MCP server configs, telemetry setting |
| VS Code (+ Cline, Roo, Continue) | Installed AI extensions, MCP configs |
| Codex CLI | MCP configs, auth token file presence |
| Gemini CLI | MCP configs, API key in config |

## Risk levels

| Level | Meaning |
|-------|---------|
| **Critical** | Direct path to data exfiltration or unintended command execution |
| **High** | Serious risk, one additional condition away from Critical |
| **Medium** | Real but limited risk; hygiene issues |
| **Low** | Informational; configuration hygiene |

Findings can be **escalated** automatically — e.g. a filesystem-access MCP server + a network-capable server on the same app are individually High, but together are escalated to Critical because they form a silent exfiltration path.

---

## Features

- **15 built-in detection rules** across MCP servers, auth files, privacy settings, and extensions
- **Context-aware escalation** — 8 escalation rules that upgrade severity for dangerous combinations
- **YAML rule packs** — define org-specific rules, override built-in severities, add custom escalations (Settings → Rule Packs)
- **Export** — Markdown, HTML (light + dark), PDF, JSON (`spec/report-schema.json`)
- **Dual platform** — native macOS (SwiftUI) and Windows (WPF) apps; identical detection logic

---

## Install

### macOS (14+)

Download `AIExposureScanner-vX.Y.Z-macos.dmg` from [Releases](../../releases), open, drag to Applications.

> First launch: right-click → Open to bypass Gatekeeper (app is unsigned — no Apple developer certificate yet).

### Windows (11)

Download `AIExposureScanner-vX.Y.Z-windows-app.zip` from [Releases](../../releases), unzip, run `AIExposureScanner.exe`.

### CLI (Windows)

```powershell
AIExposureScanner.Cli.exe --format markdown
AIExposureScanner.Cli.exe --format json > report.json
```

---

## Custom rule packs

Paste YAML in Settings → Rule Packs (macOS) or the toolbar Rule Packs window (Windows):

```yaml
version: "1.0"
name: "My Org Rules"
rules:
  - id: "ACME-001"
    description: "Internal secrets directory exposed to MCP"
    severity: high
    match:
      type: mcp_server
      path_contains: "/secrets/"
overrides:
  - id: AES-CFG-003
    severity: medium   # raise "no description" from low to medium in our org
```

Rule pack IDs must not start with `AES-`. Invalid packs are rejected with a descriptive error message — built-in rules still run.

---

## Privacy guarantee

- No network calls. The macOS entitlements keep `com.apple.security.network.client` disabled. The Windows manifest has no `internetClient` capability.
- API key values are never stored, logged, or shown unmasked — only `sk-ant-…••••••••••••` style masked previews appear.
- No telemetry, no analytics, no background update checks.
- All detection runs synchronously against the local filesystem. Detector timeout: 5 seconds per tool.

Verify yourself:
```bash
# macOS: confirm no network entitlement
codesign --display --entitlements - /Applications/AIExposureScanner.app | grep network

# Source: confirm no network imports in Scanner library
grep -r "URLSession\|URLRequest\|HttpClient\|WebClient" macos/Scanner/Sources/Scanner/ windows/Scanner/
# Expected: no output
```

---

## Build from source

### macOS (requires Xcode CLI tools or Xcode 16+)

```bash
# Run all fixture tests
swift run --package-path macos/Scanner ScannerFixtureTests

# Build app binary
swift build --package-path macos/Scanner --product AIExposureScannerApp -c release
```

### Windows (requires .NET 8 SDK)

```powershell
# Run all fixture tests
dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj

# Build CLI
dotnet publish windows/Scanner.Cli/Scanner.Cli.csproj -c Release

# Build Windows app
dotnet publish windows/AIExposureScanner.App/AIExposureScanner.App.csproj -c Release
```

### Drift check

```bash
python tools/drift-check/drift_check.py
```

Verifies every rule ID in `spec/RULES.md` is implemented in both Swift and C#. Runs on every PR.

---

## Repo layout

```
spec/              Rule catalog, detector specs, JSON report schema
fixtures/          Cross-platform golden test corpus (31 cases)
macos/Scanner/     Swift Package — Scanner library + SwiftUI app
windows/           C# .NET 8 — Scanner library, WPF app, CLI, tests
tools/drift-check/ CI parity enforcement
.github/workflows/ Build, test, drift-check, release workflows
CHANGELOG.md       Version history
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Short version:

- Rules live in `spec/RULES.md` — canonical source of truth for both platforms
- Every new rule needs: spec update + fixture cases + Swift impl + C# impl
- CI drift-check blocks PRs where rule IDs are missing in either implementation
- No network imports allowed in the Scanner library (CI enforces this)

---

## License

Apache-2.0 — see [LICENSE](LICENSE).
