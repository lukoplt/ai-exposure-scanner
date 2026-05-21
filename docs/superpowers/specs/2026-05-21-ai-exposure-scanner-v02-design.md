# AI Exposure Scanner — v0.2 Design

**Date:** 2026-05-21  
**Builds on:** v0.1 (commit 86e1a6c)

---

## Scope

Three features added to both macOS (Swift) and Windows (C#) platforms:

1. **Context scoring** — EscalationEvaluator upgrades finding severities when dangerous rule combinations co-exist on the same MCP server (per-server) or across the entire scan (global).
2. **Rule pack import** — Users load a local YAML file to add custom detection rules and override built-in severities. No network access.
3. **JSON export** — Fourth export format alongside Markdown, HTML, PDF. Conforms to `spec/report-schema.json` (updated for v0.2).

---

## 1 — Context Scoring (EscalationEvaluator)

### Pipeline change

```
ScanFacts
  → RuleEvaluator       → [Finding]
  → EscalationEvaluator → [Finding]   ← severities may be upgraded
  → ScanResult
```

`EscalationEvaluator` is a stateless value type (struct/record). It takes `[Finding]` and `[EscalationRule]` and returns a new `[Finding]` slice where upgraded findings carry an `escalationReason` string.

### EscalationRule model

```
EscalationRule {
    requiresRuleIds : Set<String>    // all must be present to trigger
    scope           : .perServer | .global
    escalateTo      : Severity
    reason          : String
}
```

**Per-server scope**: group findings by `serverName`; if a group contains every ID in `requiresRuleIds`, escalate those findings to `escalateTo`.

**Global scope**: if the full `[Finding]` list contains every ID in `requiresRuleIds` (across any server/app), escalate matching findings to `escalateTo`. A finding is only escalated if its current severity is strictly below `escalateTo`.

### Finding model change

Add one nullable field:

```
Finding {
    ...existing fields...
    escalationReason : String?   // nil = not escalated
}
```

UI shows an "Escalated ↑" chip and the reason text when this field is non-nil.

### Built-in escalation rules

| Requires | Scope | Escalate to | Reason displayed |
|----------|-------|-------------|-----------------|
| `AES-MCP-001` or `AES-MCP-003` **+** `AES-MCP-002` | per-server | Critical | Broad filesystem access combined with shell execution creates a direct exfiltration path |
| `AES-MCP-001` or `AES-MCP-003` **+** `AES-MCP-004` | per-server | Critical | Broad filesystem access combined with network capability enables silent data upload |
| `AES-MCP-002` **+** `AES-MCP-004` | per-server | Critical | Shell execution combined with network access enables remote code execution |
| `AES-AUTH-001` or `AES-AUTH-002` **+** `AES-MCP-004` | per-server | Critical | Plaintext API key combined with network access enables immediate credential exfiltration |
| `AES-MCP-002` appears on ≥ 3 distinct servers | global | High | Shell execution is exposed across three or more MCP servers — broad attack surface |

Note: the "OR" conditions in the first two rows are implemented as two separate `EscalationRule` entries sharing the same `reason`.

### Files affected

| Platform | File |
|----------|------|
| Swift | `macos/Scanner/Sources/Scanner/Models/Finding.swift` — add `escalationReason` |
| Swift | `macos/Scanner/Sources/Scanner/EscalationEvaluator.swift` — new file |
| Swift | `macos/Scanner/Sources/Scanner/EscalationRules.swift` — built-in rules |
| Swift | `macos/Scanner/Sources/Scanner/ScanOrchestrator.swift` — wire evaluator |
| C# | `windows/Scanner/Models/Finding.cs` — add `EscalationReason` |
| C# | `windows/Scanner/EscalationEvaluator.cs` — new file |
| C# | `windows/Scanner/EscalationRules.cs` — built-in rules |
| C# | `windows/Scanner/ScanOrchestrator.cs` — wire evaluator |

---

## 2 — Rule Pack Import (YAML)

### YAML schema (version 1)

```yaml
version: "1"
id: "my-org-pack"          # unique; MUST NOT start with "AES-"
name: "My Org Security Rules"

rules:
  - id: "PACK-MCP-001"     # MUST NOT start with "AES-"
    severity: critical     # low | medium | high | critical
    title: "Forbidden proxy server detected"
    explanation: "This MCP server is known to exfiltrate workspace data."
    recommendation: "Remove this server from your configuration immediately."
    match:
      fact: mcp_server     # mcp_server | setting | extension | auth_file
      # All conditions below are AND-ed. All are optional but at least one required.
      command_equals: "evil-proxy"
      command_contains: "proxy"
      name_contains: "exfil"
      args_contain: "--upload"
      app: "claude-desktop"          # limit to one app; omit = all apps

  - id: "PACK-EXT-001"
    severity: medium
    title: "Unapproved AI extension installed"
    explanation: "This extension is not approved for use in this environment."
    recommendation: "Uninstall the extension from VS Code."
    match:
      fact: extension
      extension_id_contains: "unapproved-ai"

overrides:
  - id: "AES-CFG-001"
    severity: high        # replace built-in severity

escalations:
  - requires: ["AES-MCP-002", "PACK-MCP-001"]
    scope: per_server     # per_server | global
    escalate_to: critical
    reason: "Shell execution combined with forbidden proxy server"
```

### Match conditions per fact type

| `fact` | Allowed conditions |
|--------|--------------------|
| `mcp_server` | `command_equals`, `command_contains`, `name_contains`, `args_contain`, `app` |
| `setting` | `app`, `key`, `value_equals` |
| `extension` | `app`, `extension_id_contains` |
| `auth_file` | `app` |

All string comparisons are case-insensitive substring/equality checks. No regex in v0.2.

### Validation rules

- Pack `id` and all rule `id` values must not start with `AES-` (reserved namespace).
- `severity` must be one of: `low`, `medium`, `high`, `critical`.
- Rule `id` values must be unique across all loaded packs (pack vs. pack conflict → pack rejected).
- Override target `id` must match a known built-in rule ID; unknown IDs are silently ignored (forward-compat).
- A pack that fails validation is shown with an error badge; other packs are unaffected.

### Persistence

Loaded pack contents (not file paths) are stored in:
- macOS: `UserDefaults` under key `loadedRulePacks` as JSON array of pack strings
- Windows: `%LOCALAPPDATA%\AIExposureScanner\rule-packs.json`

Storing content (not path) ensures the pack survives file moves.

### UI — Settings panel, "Rule Packs" section

```
Rule Packs
─────────────────────────────────────────────
  [My Org Rules]  3 rules · 0 overrides  [Remove]
  [⚠ Bad Pack]    Error: id starts with AES-  [Remove]
─────────────────────────────────────────────
  [Add Rule Pack…]
```

- "Add Rule Pack…" opens a file picker filtered to `.yaml`/`.yml`.
- Changes take effect on next scan.

### Files affected

| Platform | File |
|----------|------|
| Swift | `macos/Scanner/Sources/Scanner/RulePack/RulePackModel.swift` — data model |
| Swift | `macos/Scanner/Sources/Scanner/RulePack/RulePackLoader.swift` — YAML parse + validate |
| Swift | `macos/Scanner/Sources/Scanner/RulePack/RulePackEvaluator.swift` — evaluate pack rules against ScanFacts |
| Swift | `macos/Scanner/Sources/AIExposureScannerApp/AIExposureScannerApp.swift` — Settings UI section |
| C# | `windows/Scanner/RulePack/RulePackModel.cs` |
| C# | `windows/Scanner/RulePack/RulePackLoader.cs` — YamlDotNet parse + validate |
| C# | `windows/Scanner/RulePack/RulePackEvaluator.cs` |
| C# | `windows/AIExposureScanner.App/Program.cs` — Settings UI section |

**New dependencies:**
- Swift: `Yams` (MIT, SPM) — add to `macos/Scanner/Package.swift` as `.package(url: "https://github.com/jpsim/Yams.git", from: "5.0.0")`
- C#: `YamlDotNet` (MIT, NuGet) — add to `windows/Scanner/Scanner.csproj`

---

## 3 — JSON Export

### Output format

Conforms to `spec/report-schema.json`. Two schema updates required:

1. `findings[].ruleId` pattern relaxed from `^AES-[A-Z]+-[0-9]{3}$` to `^[A-Z][A-Z0-9-]*-[0-9]{3,}$` to allow custom pack rule IDs.
2. Add optional field `findings[].escalationReason` (`string | null`).

Example output:

```json
{
  "schemaVersion": "1.0.0",
  "scannerVersion": "0.2.0",
  "scannedAt": "2026-05-21T14:30:00Z",
  "platform": "macos",
  "status": "critical",
  "summary": {
    "toolsFound": 3,
    "mcpServersFound": 5,
    "critical": 2,
    "high": 1,
    "medium": 0,
    "low": 1
  },
  "findings": [
    {
      "ruleId": "AES-MCP-002",
      "severity": "critical",
      "app": "claude-desktop",
      "serverName": "filesystem",
      "extensionId": null,
      "affectedPath": "/Users/alice/.config/claude/claude_desktop_config.json",
      "maskedValue": null,
      "title": "MCP server allows shell command execution",
      "explanation": "...",
      "recommendation": "...",
      "escalationReason": "Broad filesystem access combined with shell execution creates a direct exfiltration path"
    }
  ]
}
```

### Implementation

**Swift:**
- Add `Codable` conformance to `Finding`, `ReportSummary`, `ScanResult`
- `ReportBuilder.json(_ result: ScanResult) -> String` using `JSONEncoder` with `.iso8601` date strategy and `.prettyPrinted` output option

**C#:**
- `ReportBuilder.Json(ScanResult result) -> string` using `System.Text.Json.JsonSerializer` with `JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`

**UI additions:**
- macOS export sheet: add "JSON" format option
- Windows app toolbar: add "JSON" export button
- CLI: `--format json` flag

**No new dependencies.**

### Files affected

| Platform | File |
|----------|------|
| Swift | `macos/Scanner/Sources/Scanner/Models/Finding.swift` — `Codable` |
| Swift | `macos/Scanner/Sources/Scanner/Models/ReportSummary.swift` — `Codable` |
| Swift | `macos/Scanner/Sources/Scanner/Models/ScanResult.swift` — `Codable` |
| Swift | `macos/Scanner/Sources/Scanner/Reporting/ReportBuilder.swift` — add `json()` |
| Swift | `macos/Scanner/Sources/AIExposureScannerApp/AIExposureScannerApp.swift` — export UI |
| C# | `windows/Scanner/Models/Finding.cs` — JSON attributes |
| C# | `windows/Scanner/Reporting/ReportBuilder.cs` — add `Json()` |
| C# | `windows/AIExposureScanner.App/Program.cs` — export UI |
| C# | `windows/Scanner.Cli/Program.cs` — `--format json` |
| Spec | `spec/report-schema.json` — relax ruleId pattern, add escalationReason |

---

## 4 — Fixture corpus updates

New fixture cases required for each new capability:

| Case | Tests |
|------|-------|
| `fixtures/escalation/case-01-per-server-shell-fs/` | MCP server with MCP-001 + MCP-002 → both escalated to Critical |
| `fixtures/escalation/case-02-per-server-key-network/` | AUTH-001 + MCP-004 on same server → Critical |
| `fixtures/escalation/case-03-global-shell-surface/` | 3 separate servers each with MCP-002 → all escalated to High |
| `fixtures/escalation/case-04-no-escalation/` | Combinations that do NOT meet threshold → no change |
| `fixtures/rule-pack/case-01-custom-rule-match/` | Pack with mcp_server rule → custom finding |
| `fixtures/rule-pack/case-02-override-severity/` | Pack overrides AES-CFG-001 to medium |
| `fixtures/rule-pack/case-03-pack-escalation/` | Pack-defined escalation triggers |
| `fixtures/rule-pack/case-04-invalid-pack/` | Pack with reserved AES- ID → rejected, zero findings from pack |

**Rule pack fixture format extension:** `input.json` gains an optional top-level field `"rulePacks"` — an array of YAML strings. The fixture runner parses each string as a pack and passes it to the scanner alongside the in-memory filesystem. No changes to `expected.json` format.

```json
{
  "/home/.config/cursor/settings.json": "{...}",
  "rulePacks": [
    "version: \"1\"\nid: pack-test\nname: Test Pack\nrules:\n  - id: PACK-001\n    ..."
  ]
}
```
| ~~`fixtures/json-export/`~~ | JSON output validated via CLI script in section 7 — fixture runner tests findings, not output format |

---

## 5 — spec/RULES.md updates

No new rule IDs added in v0.2 (escalation is meta-logic, not rules). Update the spec to document:
- Escalation combinations table (section to add after existing rules)
- Rule pack format reference (link to this doc)

---

## 6 — CI updates

- Drift check: no changes needed (no new rule IDs)
- Privacy grep: no changes (no network calls added)
- Add YAML dependency presence check to CI (confirm `Yams` and `YamlDotNet` resolve)

---

## 7 — Versioning

- Bump `scannerVersion` in JSON export to `"0.2.0"`
- Update `CHANGELOG.md` with `[Unreleased]` section covering all three features
- Tag `v0.2.0` after all tests pass

---

## Verification

```bash
# Swift
swift run --package-path macos/Scanner ScannerFixtureTests
# All escalation + rule-pack + json-export fixture cases must pass

# .NET
dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj
# Same fixture parity

# Drift check (should still pass — no new AES- rule IDs)
python3 tools/drift-check/drift_check.py

# JSON output validation
dotnet run --project windows/Scanner.Cli/Scanner.Cli.csproj -- --format json --output /tmp/report.json
python3 -c "
import json, jsonschema, pathlib
schema = json.loads(pathlib.Path('spec/report-schema.json').read_text())
report = json.loads(pathlib.Path('/tmp/report.json').read_text())
jsonschema.validate(report, schema)
print('JSON report valid')
"

# Manual: load a valid YAML rule pack in Settings → verify custom finding appears
# Manual: load an invalid pack (AES- ID) → verify error badge, zero custom findings
# Manual: trigger per-server escalation (MCP-001 + MCP-002 same server) → verify Critical + reason shown
```
