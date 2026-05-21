# Contributing to AI Exposure Scanner

## Dev setup

### macOS
- Xcode 16+
- Swift 6+
- `swift build --package-path macos/Scanner` to build the Scanner package
- `swift run --package-path macos/Scanner ScannerFixtureTests` to run the fixture corpus
- `swift build --package-path macos/Scanner --product AIExposureScannerApp` to build the macOS SwiftUI shell

### Windows
- Visual Studio 2022 17.9+ with "Windows App SDK" workload
- .NET 8 SDK
- `dotnet build windows/AIExposureScanner.sln` to build Scanner projects
- `dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj` to run the fixture corpus
- `dotnet run --project windows/Scanner.Cli/Scanner.Cli.csproj -- --format markdown` to run the CLI exporter

The repository-level `NuGet.config` clears external package sources. Scanner core projects currently use only the .NET SDK and must build without NuGet network access.

Scanner tests are executable fixture runners rather than framework-based test projects. They validate the golden fixture corpus, detector integration against in-memory filesystems, and report generation behavior.

Feature-to-call mapping is tracked in `docs/feature-mapping.md`. Update it whenever a product function is added, renamed, or moved between core, UI, and CLI layers.

### Drift check (both platforms)
```bash
python tools/drift-check/drift_check.py \
  --rules spec/RULES.md \
  --swift macos/Scanner/Sources/Scanner/Rules/ \
  --csharp windows/Scanner/Rules/
```

---

## Adding a new rule

Every rule must be consistent across RULES.md, fixtures, Swift implementation, and C# implementation. CI will block the PR if any piece is missing.

### Step 1 — Add to `spec/RULES.md`

Add a row to the rules table:
```
| Critical | AES-MCP-008 | My new rule | Trigger description |
```

Add a prose section with:
- `id`: `AES-MCP-008`
- Full explanation of why this is risky
- Recommended fix
- Which detectors this applies to

### Step 2 — Add fixture cases

Create at minimum:
- `fixtures/<detector>/case-XX-<slug>/input.json` — sample config that triggers the rule
- `fixtures/<detector>/case-XX-<slug>/expected.json` — expected findings output

`expected.json` format:
```json
{
  "findings": [
    {
      "ruleId": "AES-MCP-008",
      "severity": "critical",
      "app": "claude-desktop",
      "serverName": "my-server"
    }
  ]
}
```

### Step 3 — Swift implementation

Create `macos/Scanner/Sources/Scanner/Rules/AESMCP008.swift`:
```swift
public struct AESMCP008: Rule {
    public let id = "AES-MCP-008"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        // implementation
    }
}
```

Register in `RuleEvaluator.defaultRules`.

Add fixture coverage in `fixtures/`; add focused runner assertions in `macos/Scanner/Tests/ScannerFixtureTests/main.swift` if the rule needs edge-case coverage beyond the corpus.

### Step 4 — C# implementation

Create `windows/Scanner/Rules/AESMCP008.cs` — mirror of Swift logic.

Register in `RuleEvaluator.DefaultRules`.

Add equivalent fixture runner assertions in `windows/Scanner.Tests/Program.cs` when needed.

### Step 5 — Verify

```bash
python tools/drift-check/drift_check.py       # must exit 0
swift run --package-path macos/Scanner ScannerFixtureTests
dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj
```

---

## PR checklist

When opening a PR, verify each item:

- [ ] `spec/RULES.md` updated (if rule added/changed)
- [ ] `spec/detectors/*.md` updated (if detector paths/format changed)
- [ ] Fixture cases added or updated
- [ ] Swift implementation updated
- [ ] C# implementation updated (identical logic)
- [ ] `drift_check.py` passes locally
- [ ] Swift and .NET fixture runners pass locally
- [ ] No network imports added to Scanner library (`URLSession`, `HttpClient`, etc.)
- [ ] No plaintext secret values in test fixtures (use fake placeholder keys)

---

## Rule ID conventions

- Format: `AES-{CATEGORY}-{NNN}` (zero-padded 3 digits)
- Categories: `MCP`, `AUTH`, `EXT`, `CFG`
- IDs are **permanent** — deprecated rules keep their ID and get `status: deprecated` in RULES.md
- Never reuse an ID

## Privacy requirements (non-negotiable)

- Scanner library must make zero network calls
- Secret values must never appear in full in logs, UI, or exports
- Fixtures must use fake/synthetic credentials (e.g. `sk-ant-api0-FAKEFAKEFAKEFAKE`)
- No telemetry, analytics, or crash reporting may be added without explicit discussion
