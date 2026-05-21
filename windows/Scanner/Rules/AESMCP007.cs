using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESMCP007 : IRule
{
    public string Id => "AES-MCP-007";
    public Severity Severity => Severity.Medium;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .Where(server => PackageVersion.IsPackageRunner(server.CommandName()))
            .Where(server => !PackageVersion.HasPinnedVersion(server.Args))
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();
}
