using System.Text.Json;
using AIExposureScanner.Scanner;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Reporting;
using AIExposureScanner.Scanner.Rules;
using AIExposureScanner.Scanner.RulePacks;

try
{
    RunFixtureCorpus();
    RunEscalationEvaluatorTests();
    RunRuleUnitTests();
    RunDetectorIntegrationTests();
    RunReportBuilderTests();
    RunRulePackLoaderTests();
    Console.WriteLine("Scanner.Tests passed");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static void RunFixtureCorpus()
{
    var fixtureRoot = Path.Combine(FindRepoRoot(), "fixtures");
    var inputFiles = Directory
        .EnumerateFiles(fixtureRoot, "input.json", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .ToArray();

    Expect(inputFiles.Length > 0, "No fixture input files found");

    foreach (var inputFile in inputFiles)
    {
        var expectedFile = Path.Combine(Path.GetDirectoryName(inputFile)!, "expected.json");
        var appId = Directory.GetParent(Path.GetDirectoryName(inputFile)!)!.Name;
        var facts = FixtureFactBuilder.FactsFrom(inputFile, appId);
        var builtInFindings = new RuleEvaluator().Evaluate(facts);

        // Parse optional rulePacks from input.json
        var loadedPacks = new List<RulePack>();
        using (var doc2 = JsonDocument.Parse(File.ReadAllText(inputFile)))
        {
            if (doc2.RootElement.TryGetProperty("rulePacks", out var rulePacksEl))
            {
                foreach (var yamlEl in rulePacksEl.EnumerateArray())
                {
                    var yamlStr = yamlEl.GetString() ?? string.Empty;
                    if (RulePackLoader.Load(yamlStr) is RulePackLoadResult.Valid v)
                        loadedPacks.Add(v.Pack);
                }
            }
        }

        var allFindings = new RulePackEvaluator().Evaluate(facts, loadedPacks, builtInFindings);
        var escalationRules = EscalationRules.BuiltIn
            .Concat(loadedPacks.SelectMany(p => p.EscalationRules))
            .ToList();
        var escalated = new EscalationEvaluator().Evaluate(allFindings, escalationRules);

        var actual = escalated
            .Select(ComparableFinding.FromFinding)
            .ToArray();
        var expected = ExpectedReport.Load(expectedFile)
            .Findings
            .Select(ComparableFinding.FromExpected)
            .ToArray();
        var relative = Path.GetRelativePath(fixtureRoot, inputFile);

        Expect(
            actual.SequenceEqual(expected),
            $"Fixture mismatch: {relative}{Environment.NewLine}actual: {string.Join(", ", actual.Select(x => x.ToString()))}{Environment.NewLine}expected: {string.Join(", ", expected.Select(x => x.ToString()))}"
        );
    }
}

static void RunEscalationEvaluatorTests()
{
    var mcp003 = new Finding("AES-MCP-003", Severity.High, "claude-desktop", ServerName: "fs", Title: "t", Explanation: "e", Recommendation: "r");
    var mcp004 = new Finding("AES-MCP-004", Severity.High, "claude-desktop", ServerName: "fs", Title: "t", Explanation: "e", Recommendation: "r");

    var perServerRule = new EscalationRule(
        new HashSet<string>(StringComparer.Ordinal) { "AES-MCP-003", "AES-MCP-004" },
        EscalationScope.PerServer, Severity.Critical, "test reason");

    var escalated = new EscalationEvaluator().Evaluate([mcp003, mcp004], [perServerRule]);
    Expect(escalated.All(f => f.Severity == Severity.Critical && f.EscalationReason == "test reason"),
        "Per-server escalation should upgrade both findings to critical with reason");

    var notEscalated = new EscalationEvaluator().Evaluate([mcp003], [perServerRule]);
    Expect(notEscalated.All(f => f.EscalationReason is null),
        "Should not escalate when not all required rule IDs are present");

    // Different servers — per-server should NOT fire
    var serverA = new Finding("AES-MCP-003", Severity.High, "claude-desktop", ServerName: "server-a", Title: "t", Explanation: "e", Recommendation: "r");
    var serverB = new Finding("AES-MCP-004", Severity.High, "claude-desktop", ServerName: "server-b", Title: "t", Explanation: "e", Recommendation: "r");
    var splitResult = new EscalationEvaluator().Evaluate([serverA, serverB], [perServerRule]);
    Expect(splitResult.All(f => f.EscalationReason is null),
        "Per-server escalation should not fire when required rules are on different servers");

    // Global scope: 3 distinct servers
    var globalRule = new EscalationRule(
        new HashSet<string>(StringComparer.Ordinal) { "AES-MCP-004" },
        EscalationScope.Global, Severity.Critical, "global test", MinimumDistinctServers: 3);
    var findings3 = Enumerable.Range(1, 3)
        .Select(i => new Finding("AES-MCP-004", Severity.High, "claude-desktop", ServerName: $"s{i}", Title: "t", Explanation: "e", Recommendation: "r"))
        .ToArray();
    var escalatedGlobal = new EscalationEvaluator().Evaluate(findings3, [globalRule]);
    Expect(escalatedGlobal.All(f => f.Severity == Severity.Critical),
        "Global escalation should fire when 3+ distinct servers match");

    var notEscalatedGlobal = new EscalationEvaluator().Evaluate(findings3[..2], [globalRule]);
    Expect(notEscalatedGlobal.All(f => f.EscalationReason is null),
        "Global escalation should not fire with fewer than minimumDistinctServers");

    // Already at target severity — no change
    var alreadyCritical = new Finding("AES-MCP-002", Severity.Critical, "claude-desktop", ServerName: "fs", Title: "t", Explanation: "e", Recommendation: "r");
    var noChange = new EscalationEvaluator().Evaluate([alreadyCritical, mcp004], [perServerRule]);
    Expect(noChange.First(f => f.RuleId == "AES-MCP-002").EscalationReason is null,
        "Should not add escalationReason to finding already at target severity");
}

static void RunRuleUnitTests()
{
    var broadPathFacts = new ScanFacts();
    broadPathFacts.McpServers.Add(
        new McpServerFact(
            "claude-desktop",
            "documents",
            "npx",
            ["server", "/Users/testuser/Documents"],
            new Dictionary<string, string>()
        )
    );
    Expect(
        new RuleEvaluator([new AESMCP003()]).Evaluate(broadPathFacts).Select(finding => finding.RuleId).SequenceEqual(["AES-MCP-003"]),
        "AES-MCP-003 should detect broad filesystem paths"
    );

    var apiKeyArgsFacts = new ScanFacts();
    apiKeyArgsFacts.McpServers.Add(
        new McpServerFact(
            "claude-desktop",
            "arg-secret",
            "npx",
            ["sk-proj-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwx"],
            new Dictionary<string, string>()
        )
    );
    Expect(
        new RuleEvaluator([new AESAUTH002()]).Evaluate(apiKeyArgsFacts).Select(finding => finding.RuleId).SequenceEqual(["AES-AUTH-002"]),
        "AES-AUTH-002 should detect API keys in args"
    );

    var browserFacts = new ScanFacts();
    browserFacts.McpServers.Add(
        new McpServerFact("cursor", "browser", "npx", ["@playwright/mcp"], new Dictionary<string, string>())
    );
    Expect(
        new RuleEvaluator([new AESMCP005()]).Evaluate(browserFacts).Select(finding => finding.RuleId).SequenceEqual(["AES-MCP-005"]),
        "AES-MCP-005 should detect browser automation servers"
    );

    var duplicateFacts = new ScanFacts();
    duplicateFacts.McpServers.Add(new McpServerFact("claude-desktop", "filesystem", "npx", [], new Dictionary<string, string>()));
    duplicateFacts.McpServers.Add(new McpServerFact("cursor", "filesystem", "npx", [], new Dictionary<string, string>()));
    var duplicateFindings = new RuleEvaluator([new AESCFG002()]).Evaluate(duplicateFacts);
    Expect(
        duplicateFindings.Select(finding => finding.App).SequenceEqual(["claude-desktop", "cursor"]) &&
            duplicateFindings.Select(finding => finding.RuleId).SequenceEqual(["AES-CFG-002", "AES-CFG-002"]),
        "AES-CFG-002 should report each client with a duplicate server"
    );

    var missingDescriptionFacts = new ScanFacts();
    missingDescriptionFacts.McpServers.Add(new McpServerFact("windsurf", "files", "npx", [], new Dictionary<string, string>()));
    Expect(
        new RuleEvaluator([new AESCFG003()]).Evaluate(missingDescriptionFacts).Select(finding => finding.RuleId).SequenceEqual(["AES-CFG-003"]),
        "AES-CFG-003 should detect missing server descriptions"
    );

    var worldReadableFacts = new ScanFacts();
    worldReadableFacts.ConfigFiles.Add(new ConfigFileFact("cursor", "~/.cursor/mcp.json", true));
    Expect(
        new RuleEvaluator([new AESCFG004()]).Evaluate(worldReadableFacts).Select(finding => finding.RuleId).SequenceEqual(["AES-CFG-004"]),
        "AES-CFG-004 should detect world-readable config files"
    );

    var orphanedFacts = new ScanFacts();
    orphanedFacts.McpServers.Add(new McpServerFact("cursor", "old-server", "npx", [], new Dictionary<string, string>(), ConfigPath: "~/.cursor/mcp.json"));
    orphanedFacts.AppInstallations.Add(new AppInstallationFact("cursor", false));
    Expect(
        new RuleEvaluator([new AESMCP006()]).Evaluate(orphanedFacts).Select(finding => finding.RuleId).SequenceEqual(["AES-MCP-006"]),
        "AES-MCP-006 should detect MCP config left behind by an uninstalled app"
    );
}

static void RunDetectorIntegrationTests()
{
    var home = "/Users/testuser";
    var appData = Path.Combine(home, "AppData", "Roaming");
    var localAppData = Path.Combine(home, "AppData", "Local");
    string P(params string[] parts) => Path.Combine(parts);

    var fs = new InMemoryFilesystem(
        home,
        appData,
        localAppData,
        new Dictionary<string, InMemoryFile>
        {
            [P(appData, "Claude", "claude_desktop_config.json")] = new(
                """
                {
                  "mcpServers": {
                    "filesystem": {
                      "command": "npx",
                      "args": ["-y", "@modelcontextprotocol/server-filesystem@1.2.3", "C:\\Users\\testuser"],
                      "description": "Home filesystem access"
                    }
                  }
                }
                """
            ),
            [P(appData, "Cursor", "User", "settings.json")] = new("""{"cursor.privacyMode": false}"""),
            [P(home, ".codex", "config.toml")] = new(
                """
                [mcp_servers.fetch]
                command = "npx"
                args = ["-y", "@modelcontextprotocol/server-fetch"]
                description = "Fetch remote URLs"
                """
            ),
            [P(home, ".codex", "auth.json")] = new("{}"),
            [P(home, ".gemini", "settings.json")] = new("""{"apiKey":"AIzaSyFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEF","mcpServers":{}}""")
        },
        [
            P(localAppData, "AnthropicClaude"),
            P(localAppData, "Programs", "cursor"),
            P(localAppData, "Programs", "Microsoft VS Code"),
            P(home, ".vscode", "extensions", "saoudrizwan.claude-dev-2.1.0")
        ]
    );

    var result = new ScanOrchestrator().Scan(fs);
    var summary = result.Findings
        .Select(finding => $"{finding.RuleId}|{finding.App}|{finding.ServerName ?? "nil"}|{finding.ExtensionId ?? "nil"}")
        .ToHashSet(StringComparer.Ordinal);
    var expected = new HashSet<string>(StringComparer.Ordinal)
    {
        "AES-MCP-001|claude-desktop|filesystem|nil",
        "AES-CFG-001|cursor|nil|nil",
        "AES-MCP-004|codex-cli|fetch|nil",
        "AES-MCP-007|codex-cli|fetch|nil",
        "AES-AUTH-003|codex-cli|nil|nil",
        "AES-AUTH-001|gemini-cli|nil|nil",
        "AES-EXT-001|vscode|nil|saoudrizwan.claude-dev"
    };

    Expect(
        expected.IsSubsetOf(summary),
        $"Detector integration missing findings: {string.Join(", ", expected.Except(summary, StringComparer.Ordinal))}"
    );
}

static void RunReportBuilderTests()
{
    const string secret = "sk-proj-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwx";
    var facts = new ScanFacts();
    facts.McpServers.Add(
        new McpServerFact(
            "claude-desktop",
            "arg-secret",
            "node",
            [secret],
            new Dictionary<string, string>(),
            Description: "Synthetic secret argument"
        )
    );
    var result = new ScanResult(facts, new RuleEvaluator().Evaluate(facts));
    var summary = ReportSummary.FromScanResult(result);

    Expect(summary.Status == OverallStatus.Critical, "Report summary should use worst finding status");
    Expect(summary.ToolsFound == 1, "Report summary should count apps with facts");
    Expect(summary.McpServersFound == 1, "Report summary should count MCP servers");
    Expect(summary.Critical == 1, "Report summary should count critical findings");

    var builder = new ReportBuilder();
    var markdown = builder.Markdown(result, DateTimeOffset.UnixEpoch);
    var html = builder.Html(result, DateTimeOffset.UnixEpoch);
    var pdf = builder.Pdf(result, DateTimeOffset.UnixEpoch);

    Expect(markdown.Contains("# AI Exposure Scanner Report", StringComparison.Ordinal), "Markdown report should include title");
    Expect(markdown.Contains("AES-AUTH-002", StringComparison.Ordinal), "Markdown report should include rule ID");
    Expect(markdown.Contains("************", StringComparison.Ordinal), "Markdown report should include masked secret preview");
    Expect(!markdown.Contains(secret, StringComparison.Ordinal), "Markdown report must not include full secret");
    Expect(html.Contains("<!doctype html>", StringComparison.Ordinal), "HTML report should include document wrapper");
    Expect(html.Contains("AES-AUTH-002", StringComparison.Ordinal), "HTML report should include rule ID");
    Expect(html.Contains("************", StringComparison.Ordinal), "HTML report should include masked secret preview");
    Expect(!html.Contains(secret, StringComparison.Ordinal), "HTML report must not include full secret");
    Expect(pdf.Length > 8 && pdf[0] == (byte)'%' && pdf[1] == (byte)'P' && pdf[2] == (byte)'D' && pdf[3] == (byte)'F', "PDF report should include PDF header");
    Expect(!System.Text.Encoding.ASCII.GetString(pdf).Contains(secret, StringComparison.Ordinal), "PDF report must not include full secret");

    var json = builder.Json(result, scannedAt: DateTimeOffset.UnixEpoch);
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;
    Expect(root.GetProperty("schemaVersion").GetString() == "1.0.0", "JSON report should include schemaVersion");
    Expect(root.GetProperty("platform").GetString() == "windows", "JSON report should include platform");
    var jsonFindings = root.GetProperty("findings").EnumerateArray().ToList();
    Expect(jsonFindings.Any(f => f.GetProperty("ruleId").GetString() == "AES-AUTH-002"), "JSON report should include finding ruleId");
    Expect(!json.Contains(secret, StringComparison.Ordinal), "JSON report must not include full secret");
}

static void RunRulePackLoaderTests()
{
    // Valid minimal pack
    var validYaml = "version: '1.0'\nid: test-pack\nname: Test Pack\n";
    Expect(RulePackLoader.Load(validYaml) is RulePackLoadResult.Valid, "Valid minimal pack should load");

    // Reserved AES- id
    var reservedYaml = "version: '1.0'\nid: AES-CUSTOM\nname: Bad Pack\n";
    Expect(RulePackLoader.Load(reservedYaml) is RulePackLoadResult.Invalid, "Pack with AES- id should be rejected");

    // Missing version
    Expect(RulePackLoader.Load("id: test-pack\nname: X\n") is RulePackLoadResult.Invalid, "Pack missing version should be rejected");

    // Custom rule with valid match
    var packWithRule = """
        version: "1.0"
        id: my-org-rules
        name: My Org Rules
        rules:
          - id: MY-001
            severity: high
            title: Custom Rule
            explanation: Test
            recommendation: Fix it
            match:
              fact: mcp_server
              name_contains: suspicious
        """;
    var ruleResult = RulePackLoader.Load(packWithRule);
    Expect(ruleResult is RulePackLoadResult.Valid, "Pack with valid rule should load");
    if (ruleResult is RulePackLoadResult.Valid rv)
    {
        Expect(rv.Pack.Rules.Count == 1, "Pack should have 1 rule");
        Expect(rv.Pack.Rules[0].Id == "MY-001", "Rule id should be MY-001");
        Expect(rv.Pack.Rules[0].Severity.Equals("high", StringComparison.OrdinalIgnoreCase), "Rule severity should be high");
    }

    // Rule with AES- id rejected
    var badRuleYaml = """
        version: "1.0"
        id: org-rules
        name: Org
        rules:
          - id: AES-MCP-001
            severity: low
            title: Bad
            explanation: X
            recommendation: X
            match:
              fact: mcp_server
        """;
    Expect(RulePackLoader.Load(badRuleYaml) is RulePackLoadResult.Invalid, "Rule with AES- id should be rejected");

    // Override + escalation parsed and escalationRules computed
    var fullPack = """
        version: "1.0"
        id: full-pack
        name: Full Pack
        overrides:
          - id: AES-MCP-007
            severity: low
        escalations:
          - requires:
              - AES-MCP-003
              - AES-MCP-004
            scope: per_server
            escalate_to: critical
            reason: Test escalation
        """;
    var fullResult = RulePackLoader.Load(fullPack);
    Expect(fullResult is RulePackLoadResult.Valid, "Full pack should load");
    if (fullResult is RulePackLoadResult.Valid fv)
    {
        Expect(fv.Pack.Overrides.Count == 1, "Pack should have 1 override");
        Expect(fv.Pack.Escalations.Count == 1, "Pack should have 1 escalation");
        Expect(fv.Pack.EscalationRules.Count == 1, "Pack escalationRules should have 1 entry");
        Expect(fv.Pack.EscalationRules[0].EscalateTo == Severity.Critical, "Escalation target should be critical");
    }
}

static string FindRepoRoot()
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, "fixtures")) &&
            Directory.Exists(Path.Combine(current.FullName, "spec")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }

    throw new InvalidOperationException("Could not locate repository root");
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed record InMemoryFile(string Text, bool WorldReadable = false);

internal sealed class InMemoryFilesystem : IFilesystemFacade
{
    private readonly IReadOnlyDictionary<string, InMemoryFile> _files;
    private readonly HashSet<string> _directories;

    public InMemoryFilesystem(
        string homeDirectory,
        string applicationDataDirectory,
        string localApplicationDataDirectory,
        IReadOnlyDictionary<string, InMemoryFile> files,
        IEnumerable<string> directories
    )
    {
        HomeDirectory = homeDirectory;
        ApplicationDataDirectory = applicationDataDirectory;
        LocalApplicationDataDirectory = localApplicationDataDirectory;
        _files = files;
        _directories = new HashSet<string>(directories.Select(NormalizeDirectory), StringComparer.Ordinal);
    }

    public string HomeDirectory { get; }
    public string ApplicationDataDirectory { get; }
    public string LocalApplicationDataDirectory { get; }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => AllDirectories().Contains(NormalizeDirectory(path));

    public string ReadTextFile(string path, int maxBytes = 10 * 1024 * 1024) =>
        _files.TryGetValue(path, out var file)
            ? file.Text
            : throw new InvalidOperationException($"Missing in-memory file: {path}");

    public IReadOnlyList<string> ListDirectoryNames(string path)
    {
        var directory = NormalizeDirectory(path);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in AllDirectories().Where(candidate => candidate.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            var remainder = candidate[(directory.Length + 1)..];
            var first = remainder.Split(Path.DirectorySeparatorChar, 2)[0];
            names.Add(first);
        }

        foreach (var filePath in _files.Keys.Where(filePath => filePath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            var remainder = filePath[(directory.Length + 1)..];
            var first = remainder.Split(Path.DirectorySeparatorChar, 2)[0];
            names.Add(first);
        }

        return names.Order(StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<string> ListFilesRecursively(string path, int maxDepth)
    {
        var root = NormalizeDirectory(path);
        return _files.Keys
            .Where(filePath => filePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(filePath =>
            {
                var remainder = filePath[(root.Length + 1)..];
                return remainder.Split(Path.DirectorySeparatorChar).Length <= maxDepth + 1;
            })
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public bool IsWorldReadableFile(string path) =>
        _files.TryGetValue(path, out var file) && file.WorldReadable;

    private HashSet<string> AllDirectories()
    {
        var result = new HashSet<string>(_directories, StringComparer.Ordinal)
        {
            HomeDirectory,
            ApplicationDataDirectory,
            LocalApplicationDataDirectory
        };

        foreach (var directory in _directories)
        {
            InsertParentDirectories(directory, result, isFile: false);
        }

        foreach (var filePath in _files.Keys)
        {
            InsertParentDirectories(filePath, result, isFile: true);
        }

        return result;
    }

    private static void InsertParentDirectories(string path, HashSet<string> result, bool isFile)
    {
        var parts = NormalizeDirectory(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).ToList();
        if (isFile && parts.Count > 0)
        {
            parts.RemoveAt(parts.Count - 1);
        }

        var current = Path.DirectorySeparatorChar.ToString();
        foreach (var part in parts)
        {
            current = current == Path.DirectorySeparatorChar.ToString()
                ? current + part
                : Path.Combine(current, part);
            result.Add(current);
        }
    }

    private static string NormalizeDirectory(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

internal static class FixtureFactBuilder
{
    public static ScanFacts FactsFrom(string path, string appId)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var facts = new ScanFacts();

        if (root.TryGetProperty("mcpServers", out var mcpServers))
        {
            facts.McpServers.AddRange(ParseMcpServers(mcpServers, appId, path));
        }

        if (root.TryGetProperty("_type", out var typeElement))
        {
            switch (typeElement.GetString())
            {
                case "cursor-settings":
                    facts.Settings.Add(
                        new SettingFact(
                            appId,
                            "cursor.privacyMode",
                            root.TryGetProperty("cursor.privacyMode", out var privacyMode) ? privacyMode.GetBoolean() : null,
                            ConfigPath: path
                        )
                    );
                    break;
                case "cursor-mcp":
                    if (root.TryGetProperty("mcpServers", out var cursorServers))
                    {
                        facts.McpServers.AddRange(ParseMcpServers(cursorServers, appId, path));
                    }
                    break;
                case "codex-cli":
                    if (root.TryGetProperty("hasAuthFile", out var hasAuthFile) && hasAuthFile.GetBoolean())
                    {
                        facts.AuthFiles.Add(new AuthFileFact(appId, "~/.codex/auth.json"));
                    }
                    if (root.TryGetProperty("mcpServers", out var codexServers))
                    {
                        facts.McpServers.AddRange(ParseMcpServers(codexServers, appId, path));
                    }
                    break;
                case "gemini-cli-settings":
                    if (root.TryGetProperty("apiKey", out var apiKey))
                    {
                        facts.Settings.Add(new SettingFact(appId, "apiKey", StringValue: apiKey.GetString(), ConfigPath: path));
                    }
                    if (root.TryGetProperty("hasCredentialsFile", out var hasCredentialsFile) && hasCredentialsFile.GetBoolean())
                    {
                        facts.AuthFiles.Add(new AuthFileFact(appId, "~/.gemini/credentials"));
                    }
                    if (root.TryGetProperty("mcpServers", out var geminiServers))
                    {
                        facts.McpServers.AddRange(ParseMcpServers(geminiServers, appId, path));
                    }
                    break;
                case "vscode-extensions":
                    facts.Extensions.AddRange(ParseExtensions(root.TryGetProperty("extensions", out var extensions) ? extensions : default, appId));
                    break;
                case "cross-client":
                    if (root.TryGetProperty("clients", out var clients))
                    {
                        foreach (var client in clients.EnumerateObject())
                        {
                            if (client.Value.TryGetProperty("mcpServers", out var clientServers))
                            {
                                facts.McpServers.AddRange(ParseMcpServers(clientServers, client.Name, path));
                            }
                        }
                    }
                    break;
            }
        }

        if (root.TryGetProperty("configFileWorldReadable", out var worldReadable) && worldReadable.GetBoolean())
        {
            facts.ConfigFiles.Add(new ConfigFileFact(appId, path, true));
        }

        return facts;
    }

    private static IEnumerable<McpServerFact> ParseMcpServers(JsonElement servers, string appId, string configPath)
    {
        foreach (var server in servers.EnumerateObject())
        {
            var config = server.Value;
            var args = config.TryGetProperty("args", out var argsElement)
                ? argsElement.EnumerateArray().Select(arg => arg.GetString() ?? string.Empty).ToArray()
                : [];
            var env = config.TryGetProperty("env", out var envElement)
                ? envElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString() ?? string.Empty)
                : new Dictionary<string, string>();

            yield return new McpServerFact(
                appId,
                server.Name,
                config.TryGetProperty("command", out var command) ? command.GetString() ?? string.Empty : string.Empty,
                args,
                env,
                config.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean(),
                config.TryGetProperty("description", out var description) ? description.GetString() : null,
                configPath
            );
        }
    }

    private static IEnumerable<ExtensionFact> ParseExtensions(JsonElement extensions, string appId)
    {
        if (extensions.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var extensionElement in extensions.EnumerateArray())
        {
            var folder = extensionElement.GetString() ?? string.Empty;
            if (folder.StartsWith("saoudrizwan.claude-dev-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(appId, "saoudrizwan.claude-dev", "Cline", true);
            }
            else if (folder.StartsWith("rooveterinaryinc.roo-cline-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(appId, "rooveterinaryinc.roo-cline", "Roo", true);
            }
            else if (folder.StartsWith("continue.continue-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(appId, "continue.continue", "Continue", true);
            }
            else if (folder.StartsWith("github.copilot-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(appId, "github.copilot", "GitHub Copilot", false);
            }
        }
    }
}

internal sealed record ExpectedReport(IReadOnlyList<ExpectedFinding> Findings)
{
    public static ExpectedReport Load(string path)
    {
        var report = JsonSerializer.Deserialize<ExpectedReport>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        return report ?? throw new InvalidOperationException($"Could not parse expected report {path}");
    }
}

internal sealed record ExpectedFinding(
    string RuleId,
    string Severity,
    string App,
    string? ServerName,
    string? ExtensionId,
    string? EscalationReason = null
);

internal sealed record ComparableFinding(
    string RuleId,
    string Severity,
    string App,
    string? ServerName,
    string? ExtensionId,
    string? EscalationReason = null
)
{
    public static ComparableFinding FromFinding(Finding finding) =>
        new(
            finding.RuleId,
            finding.Severity.ToJsonValue(),
            finding.App,
            finding.ServerName,
            finding.ExtensionId,
            finding.EscalationReason
        );

    public static ComparableFinding FromExpected(ExpectedFinding finding) =>
        new(
            finding.RuleId,
            finding.Severity,
            finding.App,
            finding.ServerName,
            finding.ExtensionId,
            finding.EscalationReason
        );

    public override string ToString() =>
        $"{Severity} {RuleId} {App} {ServerName ?? "nil"} {ExtensionId ?? "nil"} esc={EscalationReason ?? "nil"}";
}
