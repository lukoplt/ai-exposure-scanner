## Summary

<!-- What does this PR change and why? -->

## Drift checklist

- [ ] `spec/RULES.md` updated (if rule added/changed)
- [ ] `spec/detectors/*.md` updated (if detector paths/format changed)
- [ ] Fixture cases added or updated
- [ ] Swift implementation updated
- [ ] C# implementation updated (identical logic to Swift)
- [ ] `python tools/drift-check/drift_check.py` passes locally
- [ ] Swift fixture tests pass: `swift run --package-path macos/Scanner ScannerFixtureTests`
- [ ] .NET fixture tests pass: `dotnet run --project windows/Scanner.Tests/Scanner.Tests.csproj`
- [ ] No network imports in Scanner library (`URLSession`, `HttpClient`, etc.)
- [ ] No real secret values in fixtures (use synthetic placeholders only)
