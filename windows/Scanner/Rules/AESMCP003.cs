using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESMCP003 : IRule
{
    public string Id => "AES-MCP-003";
    public Severity Severity => Severity.High;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .Where(server => server.SearchableValues().Any(PathRisk.IsBroadFilesystemPath))
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();
}
