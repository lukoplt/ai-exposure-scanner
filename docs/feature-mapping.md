# Feature Mapping

This document maps required product functions to callable implementation surfaces.

| Function | Swift Scanner core | C# Scanner core | macOS SwiftUI shell | .NET CLI |
|----------|--------------------|-----------------|---------------------|----------|
| Run local scan | `ScanOrchestrator.scan(fs:)` | `ScanOrchestrator.Scan(fs)` | Toolbar `Scan` / automatic first scan | default command |
| Read known AI tool configs | `ClaudeDesktopDetector`, `CursorDetector`, `WindsurfDetector`, `VSCodeDetector`, `CodexCliDetector`, `GeminiCliDetector` | same detector set | via scanner core | via scanner core |
| Evaluate all v0.1 rules | `RuleEvaluator.defaultRules` | `RuleEvaluator.DefaultRules` | via scanner core | via scanner core |
| Show overall status and counts | `ReportSummary(scanResult:)` | `ReportSummary.FromScanResult` | summary metrics header | included in reports |
| Filter findings by severity/app | n/a UI concern | n/a UI concern | sidebar filter pickers | n/a |
| Show finding detail | `Finding` model | `Finding` model | detail pane | report output |
| Open affected config file | `Finding.affectedPath` | `Finding.AffectedPath` | `NSWorkspace.open` detail action | path shown in report |
| Export Markdown | `ReportBuilder.markdown` | `ReportBuilder.Markdown` | toolbar export | `--format markdown` |
| Export HTML | `ReportBuilder.html` | `ReportBuilder.Html` | toolbar export | `--format html` |
| Export PDF | HTML report source | `ReportBuilder.Pdf` | WebKit PDF export | `--format pdf` |
| Settings: language | n/a UI concern | n/a UI concern | settings sheet state | n/a |
| Settings: version/link | n/a UI concern | n/a UI concern | settings sheet | n/a |
| Secret masking | rule findings only expose `maskedValue` | rule findings only expose `MaskedValue` | displays masked values only | reports masked values only |
| Config permission check | POSIX other-read bit via `LocalFilesystem.isWorldReadableFile` | POSIX other-read on Unix and broad Windows ACL principals via `LocalFilesystem.IsWorldReadableFile` | via scanner core | via scanner core |
| No network scanner core | privacy grep CI gate | privacy grep CI gate | scanner core only | scanner core only |

Additional v0.1 surfaces:
- Windows desktop shell: `windows/AIExposureScanner.App` maps scan, filters, detail view, config opening, and Markdown/HTML/PDF export to the C# scanner core.
- App-installation facts: Swift and C# detectors report installed/uninstalled app state, and `AES-MCP-006` uses that mapping to flag orphaned MCP configs.
- Release packaging: `.github/workflows/macos-release.yml` assembles a macOS app bundle/DMG with release entitlements; `.github/workflows/windows-release.yml` publishes Windows desktop and CLI zip artifacts.
