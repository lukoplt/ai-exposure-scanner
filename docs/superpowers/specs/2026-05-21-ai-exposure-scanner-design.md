# AI Exposure Scanner — Design Spec
**Date:** 2026-05-21  
**Status:** Approved for implementation planning  
**License:** Apache-2.0 (after v0.1 public release)

---

## Problem

Users adopt AI tools fast — Claude Desktop, Cursor, Windsurf, VS Code with Cline/Roo/Continue, Codex CLI, Gemini CLI — without a clear picture of what each tool can access. MCP servers, extensions, and CLI agents can have broad filesystem access, shell execution, network access, and stored credentials. No existing tool audits this in a privacy-respecting, local-only way.

## Goal

Local-only, read-only desktop audit tool. User runs scan, gets structured findings with severity, explanations, and recommended fixes. Zero data leaves the machine. Nothing is modified automatically.

---

## Decisions Made

| Dimension | Decision | Rationale |
|-----------|----------|-----------|
| Tech stack | Pure native: Swift+SwiftUI (macOS), C#+WinUI3 (Windows) | Maximum native feel, App Sandbox / MSIX signing, no FFI complexity |
| Distribution | Outside stores: notarized DMG + signed MSI/MSIX | Full Disk Access required; App Store sandbox blocks scanner's core capability |
| MVP scope | Tight: AI tool discovery + MCP config audit only | Ship small, ship right; browser + secret scanning in v0.2 |
| Rule updates | Bundled-only + manual rule pack import (no network) | Zero HTTP calls from app = trivial privacy audit |
| Scan engine | Pure native (Swift/C# code), no YAML runtime | No interpreter overhead, type-safe, simpler debugging |
| App shape | Single-window, manual scan | No background services = trust; user controls every scan |
| Persistence | In-memory only; export to file | Zero attack surface from stored state |
| Localization | EN + CS shipped; i18n framework for community additions | Primary audience is Czech dev + international devs |
| Risk scoring | Worst-finding-wins, no numeric score | Avoids false safety perception from aggregate numbers |
| OSS timing | Private dev → Apache-2.0 at v0.1 public release | Clean first impression; early community not needed for greenfield |
| Export formats | Markdown + HTML + PDF (v0.1); JSON in v0.2 | Covers audit/share/email needs; JSON deferred until CLI mode |
| Custom scan paths | No in v0.1; only known locations | Tight scope; custom paths in v0.3 project scan |
| Architecture | Two-silo (Swift + C#) + shared spec + fixture corpus | Respects "pure native"; drift controlled by CI drift-check |

---

## Architecture

### Two-silo approach

macOS (Swift) and Windows (C#) are independent implementations sharing:
- `spec/RULES.md` — canonical rule catalog (source of truth for both)
- `spec/detectors/*.md` — per-tool path specs and fact extraction definitions
- `fixtures/` — golden test corpus: sample configs → expected findings JSON
- `tools/drift-check/` — CI script validating rule IDs exist in both impls

Drift prevention:
1. Every new rule = PR to RULES.md + fixtures + Swift impl + C# impl
2. CI drift-check blocks PR if any rule ID missing in either codebase
3. Fixture tests run on both platforms; mismatch = CI fail
4. PR template requires drift checklist

### Layers (identical structure in both platforms)

```
UI layer              SwiftUI / WinUI3 XAML
ViewModel             ObservableObject / INotifyPropertyChanged
ScanOrchestrator      runs detectors in parallel, aggregates findings
[Detectors]           one per AI tool
RuleEvaluator         maps extracted Facts → Findings + severity
ReportBuilder         Markdown / HTML / PDF from findings
FilesystemFacade      testable abstraction over File I/O
```

### Key invariants

- `Scanner` library (Swift Package / .NET class lib) = pure logic, no UI deps
- Zero network code in Scanner library — enforced by CI grep gate (no URLSession/HttpClient imports)
- macOS entitlement: `com.apple.security.network.client = false` — OS-level enforcement
- Windows MSIX manifest: no `internetClient` capability
- Findings = immutable value types (structs / records)
- FilesystemFacade interface enables in-memory fixture testing without real filesystem

### Concurrency

- macOS: Swift Concurrency (`async/await` + `TaskGroup`), `ScanOrchestrator` is an `actor`
- Windows: `async/await` with `Task.WhenAll`, thread-safe findings collection

---

## Detector Inventory (v0.1)

All detectors implement the same protocol:

```swift
protocol Detector {
    var id: String { get }
    var displayName: String { get }
    func isInstalled(fs: FilesystemFacade) async -> Bool
    func collectFacts(fs: FilesystemFacade) async throws -> [Fact]
}
```

### Claude Desktop
- macOS config: `~/Library/Application Support/Claude/claude_desktop_config.json`
- Windows config: `%APPDATA%\Claude\claude_desktop_config.json`
- App check: `/Applications/Claude.app` / `%LOCALAPPDATA%\AnthropicClaude\`
- Extracts: per-MCP-server `name`, `command`, `args[]`, `env{}`, `disabled`

### Cursor
- macOS: `~/Library/Application Support/Cursor/User/settings.json` + `~/.cursor/mcp.json`
- Windows: `%APPDATA%\Cursor\User\settings.json` + `%USERPROFILE%\.cursor\mcp.json`
- App check: `/Applications/Cursor.app` / `%LOCALAPPDATA%\Programs\cursor\`
- Extracts: same as Claude Desktop (shared MCP schema) + Privacy Mode setting

### Windsurf
- macOS: `~/Library/Application Support/Windsurf/User/settings.json` + `~/.codeium/windsurf/mcp_config.json`
- Windows: `%APPDATA%\Windsurf\User\settings.json` + `%USERPROFILE%\.codeium\windsurf\mcp_config.json`
- App check: `/Applications/Windsurf.app` / `%LOCALAPPDATA%\Programs\Windsurf\`

### VS Code (+ Continue, Cline, Roo)
- macOS extensions: `~/.vscode/extensions/`
- Windows extensions: `%USERPROFILE%\.vscode\extensions\`
- Matched patterns: `continue.continue-*`, `saoudrizwan.claude-dev-*`, `rooveterinaryinc.roo-cline-*`, `github.copilot-*`
- Continue config: `~/.continue/config.json`
- Cline config: `~/Library/Application Support/Code/User/globalStorage/saoudrizwan.claude-dev/` (macOS)
- Roo config: same pattern, different extension ID

### Codex CLI
- Both platforms: `~/.codex/config.toml` + `~/.codex/auth.json`
- Extracts: MCP servers from config.toml; auth.json existence flagged (not read)

### Gemini CLI
- Both platforms: `~/.gemini/settings.json` + `~/.config/gemini/`
- Extracts: MCP servers, API key presence in plain config

### Generic MCP cross-reference (Detector 7)
- No new file scan — cross-references facts from detectors 1–6
- Flags MCP servers configured in a disabled client or duplicate across clients

### v0.1 explicit non-scope
- ❌ `.env` files in home directory
- ❌ Browser extensions
- ❌ Project/repo-level scans
- ❌ Generic entropy-based secret scanning
- ❌ Network connections / running processes
- ❌ Custom user-specified paths
- ❌ Automated fixes

---

## Rule Catalog (v0.1)

Rule IDs are permanent — deprecated rules stay in RULES.md with `status: deprecated`.

### Format: `AES-{CATEGORY}-{NNN}`

| Severity | ID | Name | Trigger |
|----------|-----|------|---------|
| Critical | AES-MCP-001 | MCP server has broad home directory access | args/env contains `~/`, `/Users/`, `C:\Users\`, `%USERPROFILE%` as root path |
| Critical | AES-MCP-002 | MCP server allows shell command execution | command is `bash/sh/zsh/cmd/powershell/pwsh` or args contain `--allow-run`/`--exec` |
| Critical | AES-AUTH-001 | API key in plain text in MCP server env | env value matches API key regex pattern |
| Critical | AES-AUTH-002 | API key in plain text in MCP server args | args element matches API key regex pattern |
| High | AES-MCP-003 | MCP server has filesystem access (broad) | args/env contains absolute path not scoped to specific project subfolder |
| High | AES-MCP-004 | MCP server has network access | command/args references fetch MCP server (`@modelcontextprotocol/server-fetch`, etc.) |
| High | AES-MCP-005 | MCP server reads browser data | command/args references playwright/puppeteer/browser-use |
| High | AES-EXT-001 | AI coding extension can access terminal | Cline/Roo/Continue detected as installed (inherent terminal access via VS Code API) |
| High | AES-CFG-001 | Cursor Privacy Mode disabled | `cursor.privacyMode: false` or value absent in older versions |
| Medium | AES-MCP-006 | Unused MCP server still configured | Server present in config but `disabled: true` or parent app not installed |
| Medium | AES-MCP-007 | MCP server runs without pinned version | command is `npx`/`uvx`/`pipx` without `@exact.version` |
| Medium | AES-AUTH-003 | Auth token file present in plain text | `~/.codex/auth.json` or `~/.gemini/credentials` exists |
| Medium | AES-CFG-002 | Overlapping MCP servers across clients | Two different clients have MCP server with same name/function |
| Low | AES-CFG-003 | MCP server has no description | Server entry lacks description field |
| Low | AES-CFG-004 | AI tool config file is world-readable | `stat` shows `o+r` (macOS) or non-restricted ACL (Windows) |

### API key regex patterns (structural match only, value never logged)

```
Anthropic:  sk-ant-[a-zA-Z0-9\-_]{80,}
OpenAI:     sk-[a-zA-Z0-9]{48}  |  sk-proj-[a-zA-Z0-9\-_]{48,}
Google AI:  AIza[a-zA-Z0-9\-_]{35}
GitHub PAT: ghp_[a-zA-Z0-9]{36}  |  github_pat_[a-zA-Z0-9_]{82}
```

### Risk aggregation

```
Overall status = max(severity) across all findings
Critical → "Critical" badge (red)
High     → "High" badge (orange)  
Medium   → "Medium" badge (yellow)
Low      → "Low" badge (blue)
(none)   → "Clean" badge (green)
```

---

## UI Flow

### Screens
1. **HomeScreen** — logo, tagline "Scan your local AI tools. Nothing leaves your computer.", "Start Scan" button, detected-tools list, settings icon
2. **ScanProgressScreen** — per-detector live status (✓ found / ⟳ checking / ◦ waiting), progress bar; skipped if scan < 0.5s
3. **ResultsScreen** — two-panel (macOS: NavigationSplitView, Windows: NavigationView + detail frame): findings list left (grouped by severity, filterable by severity + app), detail right (rule ID, severity, app, config path, why-this-matters explanation, fix recommendation, "Open config file ↗" button)
4. **ExportSheet** — modal: format selector (Markdown/HTML/PDF), masked-values notice, native save panel
5. **SettingsSheet** — language selector (English/Čeština), app version, GitHub link

### Key UX notes
- "Open config file ↗" uses `NSWorkspace.open()` / `Process.Start()` — read-only, opens in user's default editor
- "Scan Again" clears in-memory state, restarts scan
- Severity indicators use color + shape (not color alone) — WCAG 2.1 AA
- Full VoiceOver (macOS) + Narrator/UIA (Windows) support

---

## Report Formats

### Markdown
Plain text, GitHub/Slack/Confluence paste-friendly. Masked API key values: first 12 chars + `••••••••••••`.

### HTML
Self-contained (inline CSS ~8 KB, inline SVG icons, system fonts). Light + dark mode (`prefers-color-scheme`). Print CSS for physical printing. No CDN, no external resources. Generated via string template — no web engine dependency.

Implementation note: Markdown and self-contained HTML builders are implemented in the Swift and C# Scanner libraries. PDF export remains platform-specific because it depends on each desktop shell's rendering stack.

### PDF
- macOS: generate HTML → load into hidden `WKWebView` → `WKWebView.printOperationForSettings()` → PDF data → save file
- Windows: PdfSharp (MIT, .NET 6+) — direct PDF generation without browser engine

---

## Threat Model

| Threat | Mitigation |
|--------|-----------|
| Binary tampering | Apple Developer ID notarization; Azure Trusted Signing (Windows) |
| Info disclosure | API keys: structural detection only, masked in UI/exports, never logged, never sent |
| Network calls | macOS: `com.apple.security.network.client = false` entitlement (OS enforced); Windows: no `internetClient` MSIX capability; CI grep gate blocks URLSession/HttpClient imports in Scanner library |
| Elevation | No sudo, no helper daemon, no XPC with elevated rights. macOS: Full Disk Access via Privacy & Security (user-granted, revocable). Windows: standard user token. |
| DoS (symlink loop / large file) | Per-detector 5s timeout; symlink loop protection (max depth 3, visited paths set); config file 10 MB read limit |
| Repudiation | Scanner never writes files; FilesystemFacade interface rejects write calls; verified in unit tests |

---

## Distribution + Signing

### macOS
1. `xcodebuild archive` → `.xcarchive`
2. `xcodebuild -exportArchive` → signed `.app` (Developer ID Application)
3. `xcrun notarytool submit` → Apple notarization
4. `xcrun stapler staple` → staple ticket for offline Gatekeeper check
5. `hdiutil create` → DMG
6. `codesign` DMG itself
7. Upload to GitHub Release
8. (Post-v0.1) Homebrew Cask PR

### Windows
- Azure Trusted Signing ($10/month, SmartScreen-compatible)
- Artifacts: `.msix` (primary) + `.exe` via WiX 4 (enterprise fallback)
- Signing via `azure/trusted-signing-action` in GitHub Actions
- (Post-v0.1) WinGet Community Repo PR

### Updates (v0.1)
No auto-update. Settings screen shows "Check for updates" link → opens GitHub Releases page in browser. Sparkle (macOS) + WinSparkle (Windows) planned for v0.2, opt-in.

---

## Repo Structure

```
ai-exposure-scanner/
├── LICENSE
├── README.md
├── SECURITY.md
├── CONTRIBUTING.md
├── CHANGELOG.md
├── spec/
│   ├── RULES.md                    ← canonical rule catalog
│   ├── detectors/
│   │   ├── claude-desktop.md
│   │   ├── cursor.md
│   │   ├── windsurf.md
│   │   ├── vscode.md
│   │   ├── codex-cli.md
│   │   └── gemini-cli.md
│   └── report-schema.json          ← for v0.2 JSON export
├── fixtures/
│   ├── claude-desktop/
│   │   ├── case-01-broad-fs-access/
│   │   │   ├── input.json
│   │   │   └── expected.json
│   │   └── case-04-clean-config/
│   └── cursor/ windsurf/ vscode/
├── macos/
│   ├── AIExposureScanner.xcodeproj/
│   ├── AIExposureScanner/           ← SwiftUI app
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Resources/Localizable.xcstrings  ← EN + CS
│   │   └── AIExposureScanner.entitlements
│   └── Scanner/                     ← Swift Package (pure logic)
│       ├── Sources/Scanner/
│       │   ├── ScanOrchestrator.swift
│       │   ├── Detectors/
│       │   ├── Rules/
│       │   ├── Models/
│       │   ├── Reporting/
│       │   └── FilesystemFacade.swift
│       └── Tests/ScannerTests/
│           ├── FixtureTests.swift
│           └── RuleUnitTests.swift
├── windows/
│   ├── AIExposureScanner.sln
│   ├── AIExposureScanner/           ← WinUI3 app
│   │   ├── Pages/
│   │   ├── ViewModels/
│   │   └── Resources/Strings/       ← en-US/ + cs-CZ/ .resw
│   ├── Scanner/                     ← .NET class library (mirror of Swift Scanner)
│   │   ├── ScanOrchestrator.cs
│   │   ├── Detectors/
│   │   ├── Rules/
│   │   ├── Models/
│   │   ├── Reporting/
│   │   └── FilesystemFacade.cs
│   └── Scanner.Tests/               ← xUnit
│       ├── FixtureTests.cs
│       └── RuleUnitTests.cs
├── tools/
│   └── drift-check/
│       └── drift_check.py           ← parses RULES.md, verifies each ID in Swift + C#
└── .github/
    ├── PULL_REQUEST_TEMPLATE.md
    └── workflows/
        ├── macos-build-test.yml
        ├── windows-build-test.yml
        ├── drift-check.yml
        ├── macos-release.yml
        └── windows-release.yml
```

---

## CI Workflow

```yaml
# PR gating:
#   macos-build-test: xcodebuild + swift test + fixture corpus
#   windows-build-test: dotnet build + xunit + fixture corpus
#   drift-check: rule IDs in RULES.md == rule IDs in both Swift + C# code
#
# Release (tag v*.*.*):
#   macos-release: archive → notarize → staple → DMG → GitHub Release
#   windows-release: build → sign (ATS) → MSIX + WiX EXE → GitHub Release
```

---

## Roadmap

### v0.1 (~8 weeks) — MVP
macOS + Windows, 6 detectors, 15 rules, Markdown/HTML/PDF export, EN+CS, Apache-2.0 public release.

### v0.2 (~+6 weeks)
JSON export, browser extension scanner (manifest.json permissions only), secret scanning in known config locations, rule pack import (user-selected file, no network), opt-in Sparkle/WinSparkle auto-update.

### v0.3 (~+8 weeks)
Project-level scan (drag-drop folder), ignore/allowlist (persisted), CLI mode (`scan --format json`), opt-in scan history (SQLite).

### v1.0
Stable rule ID schema, reproducible builds, signed rule packs (minisign), plugin system for community detectors, known-risky MCP server hash database (bundled).

---

## Build & Verification

### macOS dev setup
```bash
xcode-select --install          # Xcode 16+
swift build --package-path macos/Scanner
swift run --package-path macos/Scanner ScannerFixtureTests
```

### Windows dev setup
```powershell
# Visual Studio 2022 17.9+ with "Windows App SDK" workload
dotnet build windows/AIExposureScanner.sln
dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj
```

### Fixture corpus test
```bash
# macOS
swift run --package-path macos/Scanner ScannerFixtureTests

# Windows
dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj
```

### Drift check
```bash
python tools/drift-check/drift_check.py \
  --rules spec/RULES.md \
  --swift macos/Scanner/Sources/Scanner/Rules/ \
  --csharp windows/Scanner/Rules/
# exits 1 if any rule ID missing in either impl
```

### Privacy verification
```bash
# Verify no network entitlement on macOS build
codesign --display --entitlements - AIExposureScanner.app \
  | grep network.client
# Expected: com.apple.security.network.client = false

# Verify no URLSession/HttpClient in Scanner library
grep -r "URLSession\|HttpClient\|URLRequest\|HttpWebRequest" \
  macos/Scanner/Sources/ windows/Scanner/
# Expected: no output
```
