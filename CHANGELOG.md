# Changelog

All notable changes to AI Exposure Scanner are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.2.0] — 2026-05-21

### Added
- **Context-aware escalation scoring**: `EscalationEvaluator` post-processes findings after rule evaluation to upgrade severities for dangerous rule combinations. 8 built-in escalation rules covering per-server and global cross-server risks. `escalationReason` field on `Finding` surfaces the reason in UI and exports.
- **YAML rule packs**: Organizations can define custom detection rules, severity overrides for built-in rules, and custom escalation rules via YAML files. Full validation with descriptive error messages. Persistence via UserDefaults (macOS) and `%LOCALAPPDATA%\AIExposureScanner\rule-packs.json` (Windows).
- **JSON export**: `ReportBuilder.json()` on both platforms emitting reports conforming to `spec/report-schema.json`. `--format json` added to Windows CLI. JSON export buttons added to macOS and Windows apps.
- Rule Packs management UI in macOS Settings sheet (paste YAML, enable/disable, remove packs).
- Rule Packs management window in Windows app toolbar (paste YAML, remove packs).
- 8 new fixture cases covering escalation scenarios and rule pack features.
- Yams 5.x (Swift) and YamlDotNet 17.x (C#) YAML parsing dependencies.

### Changed
- `spec/report-schema.json`: relaxed `ruleId` pattern to allow custom rule pack IDs; added `escalationReason` field to finding schema.
- Version bumped to 0.2.0 in app UIs.

## [0.1.0] — 2026-05-21

First public release.
