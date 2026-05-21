# AI Exposure Scanner

> Local-only security audit for your AI tools. Nothing leaves your computer.

AI Exposure Scanner finds installed AI tools (Claude Desktop, Cursor, Windsurf, VS Code extensions, Codex CLI, Gemini CLI), audits their MCP server configurations for risky permissions, and reports plaintext credentials — with clear explanations and recommended fixes.

**No cloud. No account. No telemetry. Zero network calls.**

## Current implementation status

The repository contains the v0.1 implementation: shared rule catalog, fixture corpus, Swift Scanner package, C# Scanner library, local filesystem abstractions, known-path detectors, scan orchestrators, Markdown/HTML report builders, a macOS SwiftUI app shell with PDF export, a Windows desktop shell, and a .NET CLI exporter. See [feature mapping](docs/feature-mapping.md).

---

## What it checks

| Tool | What's audited |
|------|----------------|
| Claude Desktop | MCP server configs, filesystem access scope, shell execution, credentials in env |
| Cursor | MCP server configs, Privacy Mode setting |
| Windsurf | MCP server configs |
| VS Code (+ Cline, Roo, Continue) | Installed AI extensions, MCP configs |
| Codex CLI | MCP configs, auth token file presence |
| Gemini CLI | MCP configs, API key in config |

## Risk levels

- **Critical** — Direct path to data exfiltration or unintended command execution
- **High** — Serious risk requiring one additional condition
- **Medium** — Real but limited risk; hygiene issues
- **Low** — Informational; configuration hygiene

---

## Install

### macOS (14+)

Download `AIExposureScanner-vX.Y.Z.dmg` from [Releases](https://github.com/ai-exposure-scanner/ai-exposure-scanner/releases), open, drag to Applications.

> The app requires Full Disk Access to read AI tool configs. You'll be prompted on first scan.

```bash
# Homebrew (after Cask PR is merged)
brew install --cask ai-exposure-scanner
```

### Windows (10+)

Download `AIExposureScanner-vX.Y.Z-windows-app.zip` from [Releases](https://github.com/ai-exposure-scanner/ai-exposure-scanner/releases), unzip it, and run `AIExposureScanner.exe`.

```powershell
# WinGet (after WinGet Community Repo PR is merged)
winget install ai-exposure-scanner.AIExposureScanner
```

---

## Privacy guarantee

- No network calls. The macOS release entitlements explicitly keep `com.apple.security.network.client` and `com.apple.security.network.server` disabled.
- The Windows app has no network capability declaration, and the v0.1 MSIX manifest declaration contains no `internetClient` capability.
- API keys and secrets are detected structurally (prefix + length + charset). Their values are never logged, never stored, never shown in full — only masked previews appear in the UI and exports.
- No telemetry. No analytics. No update checks in the background.

Verify yourself:
```bash
# macOS
codesign --display --entitlements - /Applications/AIExposureScanner.app | grep network
# Expected: network client/server entitlements are false or absent
```

---

## Build from source

### macOS

```bash
# Requires Xcode 16+
git clone https://github.com/ai-exposure-scanner/ai-exposure-scanner
cd ai-exposure-scanner
swift build --package-path macos/Scanner
swift run --package-path macos/Scanner ScannerFixtureTests
swift build --package-path macos/Scanner --product AIExposureScannerApp
# Launch locally when you want the SwiftUI shell:
swift run --package-path macos/Scanner AIExposureScannerApp
```

### Windows

```powershell
# Requires Visual Studio 2022 17.9+ with "Windows App SDK" workload
git clone https://github.com/ai-exposure-scanner/ai-exposure-scanner
cd ai-exposure-scanner
dotnet build windows/AIExposureScanner.sln
dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj
dotnet run --project windows/Scanner.Cli/Scanner.Cli.csproj -- --format markdown
dotnet run --project windows/AIExposureScanner.App/AIExposureScanner.App.csproj
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Key things:

- Rules live in `spec/RULES.md` — the canonical source of truth for both platforms
- Every new rule needs: `spec/RULES.md` update + `fixtures/` test cases + Swift impl + C# impl
- CI drift-check blocks PRs where rule IDs are missing in either implementation

---

## License

Apache-2.0 — see [LICENSE](LICENSE).
