using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESCFG002 : IRule
{
    public string Id => "AES-CFG-002";
    public Severity Severity => Severity.Medium;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .GroupBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(server => server.AppId).Distinct(StringComparer.Ordinal).Count() > 1)
            .SelectMany(group => group.Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath)))
            .ToArray();
}
