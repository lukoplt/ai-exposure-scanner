# AI Exposure Scanner

> Local-only security audit for your AI developer tools. **Nothing leaves your computer.**

AI Exposure Scanner finds installed AI tools (Claude Desktop, Claude CLI, Cursor, Windsurf, VS Code, Codex CLI, Gemini CLI), audits their MCP server configurations for dangerous permissions, detects exposed credentials, and surfaces privacy setting issues — with severity ratings, clear explanations, and recommended fixes.

**No cloud. No account. No telemetry. Zero background network calls.**

The only network request the app can ever make is an *opt-in* update check (default OFF) that runs in a separate sandboxed helper binary — see [Privacy guarantee](#privacy-guarantee) for the full threat model.

---

## Why

Every modern AI coding assistant ships with a config file full of MCP servers, API keys, filesystem scopes, and shell-execution capabilities. Most users have no idea what their tools are allowed to do — and "filesystem access to `~/Documents`" plus "shell execution" plus "network fetch" combined silently equals "anything on your machine can be uploaded anywhere."

This tool reads those configs and tells you, in plain language, what risks they expose.

---

## What it checks

| Tool | Config path | What's audited |
|------|-------------|----------------|
| Claude Desktop | `claude_desktop_config.json` | MCP server configs, filesystem scope, shell exec, env credentials |
| Claude CLI (Claude Code) | `~/.claude.json` | Same MCP rules as Desktop |
| Cursor | `settings.json` | MCP server configs, Privacy Mode setting |
| Windsurf | MCP config | MCP server configs, telemetry setting |
| VS Code | `extensions/` + MCP configs | Installed AI extensions (Cline, Roo, Continue), MCP configs |
| Codex CLI | `~/.codex/` | MCP configs, auth token file presence |
| Gemini CLI | `~/.gemini/` | MCP configs, API key stored in config |

## Risk levels

| Level | Meaning |
|-------|---------|
| **Critical** | Direct path to data exfiltration or unintended command execution |
| **High** | Serious risk, one additional condition away from Critical |
| **Medium** | Real but limited risk; hygiene issues |
| **Low** | Informational; configuration hygiene |

Findings are **automatically escalated** when a dangerous combination appears — e.g. a filesystem-access MCP server plus a network-capable server on the same app is individually High, but together is escalated to Critical because that forms a silent exfiltration path.

---

## Features (v0.2.0)

- **15 built-in detection rules** across MCP servers, auth files, privacy settings, and extensions
- **8 escalation rules** that upgrade severity for dangerous combinations
- **7 detectors** including the new Claude CLI (Claude Code) integration
- **YAML rule packs** — define org-specific rules, override built-in severities, add custom escalations (Settings → Rule Packs)
- **Secret masking** — API key values never appear unmasked; only the schema prefix is shown (`sk-ant-••••••••`, `AIza••••••••`)
- **Export formats** — Markdown, HTML (light + dark mode), PDF, JSON ([`spec/report-schema.json`](spec/report-schema.json))
- **Localization** — UI auto-detects system language (English / Čeština) and persists user choice
- **Dual platform** — native macOS (SwiftUI) and Windows (WPF). Identical detection logic, same fixture corpus.

---

## Install

### macOS 14+ (prebuilt DMG)

1. Download `AIExposureScanner-v0.2.0-macos.dmg` from the [latest release](../../releases/latest).
2. Open the DMG, drag the app onto the **Applications** shortcut.
3. **First launch:** right-click the app → **Open** to bypass Gatekeeper.

> The DMG is currently **unsigned** (no Apple Developer certificate yet). Gatekeeper will warn on first launch. Once an Apple Developer account is set up the release pipeline will automatically sign + notarize.

### Windows 10/11 (build from source — no prebuilt installer yet)

There is **no prebuilt Windows installer in v0.2.0**. To run the Windows app, build it yourself on a Windows machine — instructions below in [Build from source → Windows](#windows-net-10-sdk-required).

The Scanner library, CLI, WPF app, and full test suite all build cleanly on .NET 10. CI on `windows-2025-vs2026` runners verifies this on every push.

A signed prebuilt installer will land in a later release once Windows code-signing is set up.

---

## Privacy guarantee

The app makes a credible "nothing leaves your computer" claim. Here is exactly how that is enforced:

### OS-level enforcement (the strongest layer)

- **macOS:** the app ships with an `entitlements` file that explicitly sets `com.apple.security.network.client = false` and `com.apple.security.network.server = false`. Once signed + sandboxed, the operating system **refuses** to let the process open a network socket. Verify with:
  ```bash
  codesign --display --entitlements - /Applications/AIExposureScanner.app | grep network
  ```
- **Windows:** the MSIX `Package.appxmanifest` declares an empty `<Capabilities />` element — no `internetClient`, no `internetClientServer`. Confirms with `appxmanifest` inspection.

### Source-level enforcement

The Scanner library (which does all the actual detection) imports **zero** network primitives — no `URLSession`, no `URLRequest`, no `HttpClient`, no `WebClient`, no `HttpWebRequest`. The CI privacy-grep job blocks any PR that re-introduces them. Verify yourself:

```bash
grep -r "URLSession\|URLRequest\|HttpClient\|WebClient\|HttpWebRequest" \
  macos/Scanner/Sources/Scanner/ windows/Scanner/
# Expected: no output
```

### Secret handling

API keys discovered in MCP server `env` blocks or in `apiKey` settings are **never** stored, logged, or shown unmasked. Reports include only the schema prefix (`sk-ant-`, `sk-proj-`, `ghp_`, `AIza`, `github_pat_`, `sk-`) plus 24 bullet characters. The 8 characters of token-randomness leaked by the old mask are no longer visible anywhere.

### The single intentional exception: opt-in update check

Auto-update is **off by default**. When the user explicitly turns it on in Settings, the main app spawns a separate helper binary (`AIExposureUpdater`) that does exactly one thing: a single HTTPS `GET https://api.github.com/repos/lukoplt/ai-exposure-scanner/releases/latest`. The result is written to `~/Library/Caches/com.aiexposurescanner.app/update.json` and the main app reads it on next launch.

- The main app and the Scanner library remain network-free.
- The updater is a ~120-line auditable binary in [`macos/Scanner/Sources/AIExposureUpdater/main.swift`](macos/Scanner/Sources/AIExposureUpdater/main.swift).
- Nothing is auto-downloaded. The banner just links to the GitHub Releases page; the user manually downloads any new DMG.

### What we do not collect

- No telemetry, no analytics, no crash reports, no error tracking.
- No background pings on any schedule.
- No anonymous usage stats.
- No "first-run welcome" home-call.

The repo contains zero references to Sentry, Crashlytics, Firebase, Google Analytics, Mixpanel, PostHog, Datadog, or any other observability SaaS. `grep` for them yourself.

---

## Custom rule packs

In **Settings → Rule Packs** (macOS) or the **Rule Packs** toolbar window (Windows), paste a YAML document:

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
    severity: medium   # raise "no description" from low to medium for this org
```

Rule pack IDs must not start with `AES-` (reserved for built-ins). Invalid packs are rejected with a descriptive error message — built-in rules continue to run.

**The Add Rule Pack dialog refuses YAML that contains a recognized API-key pattern** so users do not accidentally paste live credentials into the rule-pack store (which is plaintext on disk).

---

## Build from source

### macOS (Xcode 16+ or Swift 6 toolchain)

```bash
git clone https://github.com/lukoplt/ai-exposure-scanner.git
cd ai-exposure-scanner

# Run the full fixture corpus (31 cases across all detectors)
swift run --package-path macos/Scanner ScannerFixtureTests

# Build the GUI app
swift build --package-path macos/Scanner --configuration release --product AIExposureScannerApp

# Build the standalone updater (optional — opt-in feature)
swift build --package-path macos/Scanner --configuration release --product AIExposureUpdater
```

The release binary lands at `macos/Scanner/.build/arm64-apple-macosx/release/AIExposureScannerApp`. Assemble it into a `.app` bundle by following the recipe in [`.github/workflows/macos-release.yml`](.github/workflows/macos-release.yml).

### Windows (.NET 10 SDK required)

```powershell
git clone https://github.com/lukoplt/ai-exposure-scanner.git
cd ai-exposure-scanner\windows

# Wipe macOS-side build artifacts before first build on Windows
Get-ChildItem -Recurse -Include bin,obj | Remove-Item -Recurse -Force

# Restore + build everything
dotnet restore AIExposureScanner.sln
dotnet build AIExposureScanner.sln -c Release

# Run the fixture corpus
dotnet run --project Scanner.Tests\Scanner.Tests.csproj -c Release

# Build a self-contained single-file Windows app (no .NET runtime needed on target)
dotnet publish AIExposureScanner.App\AIExposureScanner.App.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist\windows-app
# Result: dist\windows-app\AIExposureScanner.exe

# Build the CLI
dotnet publish Scanner.Cli\Scanner.Cli.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o dist\windows-cli
# Result: dist\windows-cli\AIExposureScanner.Cli.exe
```

**Prerequisites:**
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the project explicitly targets `net10.0-windows` for the WPF app and `net10.0` for the library/CLI/tests)
- PowerShell 7+ (or `cmd.exe`)

**WPF requires Windows.** You cannot cross-compile the GUI app from macOS — only the library, CLI, and tests build on macOS via .NET cross-platform support.

### CLI usage (Windows)

```powershell
AIExposureScanner.Cli.exe --format markdown                 # human-readable report on stdout
AIExposureScanner.Cli.exe --format json > report.json       # machine-readable for CI
AIExposureScanner.Cli.exe --format html > report.html
AIExposureScanner.Cli.exe --help
```

### Drift check (cross-platform parity)

```bash
python3 tools/drift-check/drift_check.py
```

Verifies every rule ID in `spec/RULES.md` is implemented in both Swift and C#. Runs on every PR.

---

## Repo layout

```
spec/              Rule catalog, detector specs, JSON report schema
fixtures/          Cross-platform golden test corpus (31 cases)
macos/Scanner/     Swift Package — Scanner library, SwiftUI app, opt-in updater
windows/           C# .NET 10 — Scanner library, WPF app, CLI, tests
tools/drift-check/ CI parity enforcement (Python)
.github/workflows/ Build, test, drift-check, macOS release, Windows release
CHANGELOG.md       Version history
NOTICE             Apache-2.0 attribution
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Short version:

- Rules live in `spec/RULES.md` — canonical source of truth for both platforms
- Every new rule needs: spec update + fixture cases + Swift impl + C# impl
- CI drift-check blocks PRs where rule IDs are missing in either implementation
- No network imports allowed in the Scanner library (CI privacy-grep enforces this)
- No real secret values in fixtures — use synthetic placeholders only

---

## Threat model summary

| Threat | Mitigation |
|--------|------------|
| App phones home with config contents | OS-level network entitlement denied + source-level grep CI gate |
| Auto-updater leaks usage data | Opt-in only, default OFF, runs as separate auditable binary |
| Reports leak API keys when shared | Mask reveals only schema prefix (`sk-ant-`, `AIza`, etc.) + bullets |
| Malicious rule pack stores secrets in plaintext | `SecretPatterns.ContainsSecret` pre-save scan rejects YAML with key patterns |
| Supply chain — compromised CI action | All GitHub Actions versioned via floating major tags (`@v5`, `@v6`) — accept supply-chain risk in exchange for getting CVE fixes automatically |
| Supply chain — compromised YAML lib | `YamlDotNet 17.0.0` pinned (no float); Yams locked via `Package.resolved` |
| Symlink loop on broad scan | Both `FilesystemFacade` implementations skip junctions/symlinks before recursing |
| Path-injection RCE via Affected Path | Windows `Process.Start` uses `explorer.exe /select,…` with `UseShellExecute=false` after `IsPathFullyQualified` + `File.Exists` validation |

A full security audit (CSO format) lives in commit `0c3ba4f` and the corresponding fixes are tagged in the v0.2.0 changelog.

---

## License

Apache-2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE).

If you fork, vendor, or redistribute, please preserve the `NOTICE` file and credit `Lukáš Oplt` plus a link back to https://github.com/lukoplt/ai-exposure-scanner.

---

## Support

Built and maintained by **Lukáš Oplt**.

If this tool saved you from an avoidable AI-config security incident, consider buying me a coffee: <https://www.buymeacoffee.com/lukasoplt> ☕
