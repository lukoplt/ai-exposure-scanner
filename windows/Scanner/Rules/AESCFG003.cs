using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESCFG003 : IRule
{
    public string Id => "AES-CFG-003";
    public Severity Severity => Severity.Low;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .Where(server => string.IsNullOrWhiteSpace(server.Description))
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();
}
