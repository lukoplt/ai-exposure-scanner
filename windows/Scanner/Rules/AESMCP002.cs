using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESMCP002 : IRule
{
    private static readonly HashSet<string> Shells = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash",
        "sh",
        "zsh",
        "fish",
        "cmd",
        "cmd.exe",
        "powershell",
        "powershell.exe",
        "pwsh",
        "pwsh.exe"
    };

    public string Id => "AES-MCP-002";
    public Severity Severity => Severity.Critical;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .Where(Triggers)
            .Select(server => this.Finding(server.AppId, server.Name, affectedPath: server.ConfigPath))
            .ToArray();

    private static bool Triggers(McpServerFact server)
    {
        var commandIsShell = Shells.Contains(server.CommandName());
        var hasExecutionArg = server.Args.Any(arg =>
        {
            var lower = arg.ToLowerInvariant();
            return lower is "--allow-run" or "--exec" ||
                lower.Contains("execute", StringComparison.Ordinal) ||
                (commandIsShell && lower == "-c");
        });

        return commandIsShell || hasExecutionArg;
    }
}
