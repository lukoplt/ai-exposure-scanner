using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner;

public static class EscalationRules
{
    private const string FsShell   = "Broad filesystem access combined with shell execution creates a direct exfiltration path";
    private const string FsNet     = "Broad filesystem access combined with network capability enables silent data upload";
    private const string ShellNet  = "Shell execution combined with network access enables remote code execution";
    private const string KeyNet    = "Plaintext API key combined with network access enables immediate credential exfiltration";
    private const string GlobalNet = "Network access capability is exposed across three or more MCP servers — broad attack surface";

    public static readonly IReadOnlyList<EscalationRule> BuiltIn =
    [
        new(S("AES-MCP-001", "AES-MCP-002"), EscalationScope.PerServer, Severity.Critical, FsShell),
        new(S("AES-MCP-003", "AES-MCP-002"), EscalationScope.PerServer, Severity.Critical, FsShell),
        new(S("AES-MCP-001", "AES-MCP-004"), EscalationScope.PerServer, Severity.Critical, FsNet),
        new(S("AES-MCP-003", "AES-MCP-004"), EscalationScope.PerServer, Severity.Critical, FsNet),
        new(S("AES-MCP-002", "AES-MCP-004"), EscalationScope.PerServer, Severity.Critical, ShellNet),
        new(S("AES-AUTH-001", "AES-MCP-004"), EscalationScope.PerServer, Severity.Critical, KeyNet),
        new(S("AES-AUTH-002", "AES-MCP-004"), EscalationScope.PerServer, Severity.Critical, KeyNet),
        new(S("AES-MCP-004"), EscalationScope.Global, Severity.Critical, GlobalNet, MinimumDistinctServers: 3),
    ];

    private static IReadOnlySet<string> S(params string[] ids) =>
        new HashSet<string>(ids, StringComparer.Ordinal);
}
