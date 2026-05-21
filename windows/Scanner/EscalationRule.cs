using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner;

public enum EscalationScope { PerServer, Global }

public sealed record EscalationRule(
    IReadOnlySet<string> RequiresRuleIds,
    EscalationScope Scope,
    Severity EscalateTo,
    string Reason,
    int MinimumDistinctServers = 1
);
