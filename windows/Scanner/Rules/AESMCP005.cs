using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESMCP005 : IRule
{
    private static readonly string[] BrowserMarkers =
    [
        "playwright",
        "puppeteer",
        "browser-use",
        "selenium",
        "@playwright/mcp",
        "playwright-mcp"
    ];

    public string Id => "AES-MCP-005";
    public Severity Severity => Severity.High;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .Where(server => server.SearchableValues()
                .Select(value => value.ToLowerInvariant())
                .Any(value => BrowserMarkers.Any(value.Contains)))
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();
}
