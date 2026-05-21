using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.RulePacks;

public sealed record RulePack(string Version, string Id, string Name,
    IReadOnlyList<PackRule> Rules, IReadOnlyList<PackOverride> Overrides, IReadOnlyList<PackEscalation> Escalations)
{
    public IReadOnlyList<EscalationRule> EscalationRules =>
        Escalations
            .Select(pe =>
            {
                if (!Enum.TryParse<EscalationScope>(ToPascal(pe.Scope), out var scope)) return null;
                if (!TryParseSeverity(pe.EscalateTo, out var escalateTo)) return null;
                return new EscalationRule(
                    new HashSet<string>(pe.Requires, StringComparer.Ordinal),
                    scope, escalateTo, pe.Reason);
            })
            .Where(r => r is not null)
            .Select(r => r!)
            .ToList();

    private static string ToPascal(string s) =>
        string.Concat(s.Split('_').Select(w => w.Length > 0 ? char.ToUpperInvariant(w[0]) + w[1..] : ""));

    private static bool TryParseSeverity(string value, out Severity severity)
    {
        switch (value.ToLowerInvariant())
        {
            case "critical": severity = Severity.Critical; return true;
            case "high":     severity = Severity.High;     return true;
            case "medium":   severity = Severity.Medium;   return true;
            case "low":      severity = Severity.Low;      return true;
            default:         severity = default;           return false;
        }
    }
}

public sealed record PackRule(string Id, string Severity, string Title,
    string Explanation, string Recommendation, PackRuleMatch Match);

public sealed record PackRuleMatch(string Fact,
    string? CommandEquals = null, string? CommandContains = null,
    string? NameContains = null, string? ArgsContain = null,
    string? App = null, string? Key = null,
    string? ValueEquals = null, string? ExtensionIdContains = null);

public sealed record PackOverride(string Id, string Severity);

public sealed record PackEscalation(IReadOnlyList<string> Requires,
    string Scope, string EscalateTo, string Reason);
