using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner;

public sealed class EscalationEvaluator
{
    public IReadOnlyList<Finding> Evaluate(IReadOnlyList<Finding> findings, IReadOnlyList<EscalationRule> rules)
    {
        var result = findings.ToList();
        foreach (var rule in rules)
        {
            result = Apply(rule, result);
        }
        return result;
    }

    private static List<Finding> Apply(EscalationRule rule, List<Finding> findings) =>
        rule.Scope switch
        {
            EscalationScope.PerServer => ApplyPerServer(rule, findings),
            EscalationScope.Global    => ApplyGlobal(rule, findings),
            _                         => findings
        };

    private static List<Finding> ApplyPerServer(EscalationRule rule, List<Finding> findings)
    {
        var groups = findings
            .GroupBy(f => $"{f.App}||{f.ServerName ?? string.Empty}", StringComparer.Ordinal)
            .ToList();

        var result = findings.ToList();
        foreach (var group in groups)
        {
            var ruleIds = group.Select(f => f.RuleId).ToHashSet(StringComparer.Ordinal);
            if (!rule.RequiresRuleIds.IsSubsetOf(ruleIds)) continue;

            result = result
                .Select(f =>
                    group.Contains(f) &&
                    rule.RequiresRuleIds.Contains(f.RuleId) &&
                    (int)f.Severity > (int)rule.EscalateTo
                        ? f with { Severity = rule.EscalateTo, EscalationReason = rule.Reason }
                        : f)
                .ToList();
        }
        return result;
    }

    private static List<Finding> ApplyGlobal(EscalationRule rule, List<Finding> findings)
    {
        var allRuleIds = findings.Select(f => f.RuleId).ToHashSet(StringComparer.Ordinal);
        if (!rule.RequiresRuleIds.IsSubsetOf(allRuleIds)) return findings;

        if (rule.MinimumDistinctServers > 1)
        {
            foreach (var reqId in rule.RequiresRuleIds)
            {
                var distinctServers = findings
                    .Where(f => f.RuleId == reqId && f.ServerName is not null)
                    .Select(f => f.ServerName!)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                if (distinctServers < rule.MinimumDistinctServers) return findings;
            }
        }

        return findings
            .Select(f =>
                rule.RequiresRuleIds.Contains(f.RuleId) && (int)f.Severity > (int)rule.EscalateTo
                    ? f with { Severity = rule.EscalateTo, EscalationReason = rule.Reason }
                    : f)
            .ToList();
    }
}
