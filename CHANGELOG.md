# Changelog

All notable changes to AI Exposure Scanner are documented here.
Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Product documentation, rule catalog, detector specs, report schema, and security policy
- Initial macOS Swift Scanner package and Windows C# Scanner library
- 15 native rule evaluators covering MCP filesystem access, shell execution, network access, plaintext credentials, extension risk, and config hygiene
- Local filesystem abstraction, known-path detectors, and scan orchestrators for Swift and .NET
- Report summary, Markdown report builder, and self-contained HTML report builder in Swift and .NET
- macOS SwiftUI app shell wired to scan, filters, details, config-file opening, settings, and Markdown/HTML/PDF export
- Windows desktop shell wired to scan, filters, details, config-file opening, and Markdown/HTML/PDF export
- .NET CLI exporter for Markdown/HTML/PDF reports
- App-installation facts for known tools and orphaned MCP config detection
- macOS release plist/entitlements plus macOS and Windows release workflows
- Feature mapping document for callable product surfaces
- Cross-platform fixture corpus and executable fixture runners for Swift and .NET
- Drift-check tooling and Core CI workflow

## [0.1.0] — 2026-05-21

First public release.
