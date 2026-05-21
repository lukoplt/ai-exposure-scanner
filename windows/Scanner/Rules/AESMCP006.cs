using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESMCP006 : IRule
{
    public string Id => "AES-MCP-006";
    public Severity Severity => Severity.Medium;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts)
    {
        var uninstalledAppIds = facts.AppInstallations
            .Where(installation => !installation.Installed)
            .Select(installation => installation.AppId)
            .ToHashSet(StringComparer.Ordinal);

        return facts.McpServers
            .Where(server => server.Disabled || uninstalledAppIds.Contains(server.AppId))
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();
    }
}
