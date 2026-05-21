using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESMCP004 : IRule
{
    private static readonly HashSet<string> NetworkCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "curl",
        "wget",
        "httpie"
    };

    public string Id => "AES-MCP-004";
    public Severity Severity => Severity.High;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .Where(Triggers)
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();

    private static bool Triggers(McpServerFact server)
    {
        var values = server.SearchableValues().Select(value => value.ToLowerInvariant()).ToArray();
        return NetworkCommands.Contains(server.CommandName()) ||
            values.Any(value =>
                value.Contains("@modelcontextprotocol/server-fetch", StringComparison.Ordinal) ||
                value.Contains("mcp-server-fetch", StringComparison.Ordinal)
            );
    }
}
