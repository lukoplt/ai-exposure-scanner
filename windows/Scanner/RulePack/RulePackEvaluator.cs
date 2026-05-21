using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.RulePacks;

public sealed class RulePackEvaluator
{
    public IReadOnlyList<Finding> Evaluate(
        ScanFacts facts,
        IReadOnlyList<RulePack> packs,
        IReadOnlyList<Finding> existing)
    {
        var findings = new List<Finding>(existing);

        foreach (var pack in packs)
        {
            // 1. Custom rules → new findings
            foreach (var rule in pack.Rules)
                findings.AddRange(MatchRule(rule, facts));

            // 2. Overrides — replace severity on matched rule IDs
            var overrideMap = pack.Overrides.ToDictionary(o => o.Id, o => o.Severity, StringComparer.Ordinal);
            if (overrideMap.Count > 0)
            {
                for (var i = 0; i < findings.Count; i++)
                {
                    var f = findings[i];
                    if (overrideMap.TryGetValue(f.RuleId, out var newSev)
                        && Enum.TryParse<Severity>(newSev, ignoreCase: true, out var parsedSev))
                    {
                        findings[i] = f with { Severity = parsedSev };
                    }
                }
            }
        }

        return findings;
    }

    // MARK: - Match

    private static IEnumerable<Finding> MatchRule(PackRule rule, ScanFacts facts)
    {
        if (!Enum.TryParse<Severity>(rule.Severity, ignoreCase: true, out var severity))
            yield break;

        switch (rule.Match.Fact.ToLowerInvariant())
        {
            case "mcp_server":
                foreach (var s in facts.McpServers.Where(s => MatchesServer(s, rule.Match)))
                    yield return new Finding(rule.Id, severity, s.AppId,
                        ServerName: s.Name, AffectedPath: s.ConfigPath,
                        Title: rule.Title, Explanation: rule.Explanation, Recommendation: rule.Recommendation);
                break;

            case "setting":
                foreach (var s in facts.Settings.Where(s => MatchesSetting(s, rule.Match)))
                    yield return new Finding(rule.Id, severity, s.AppId,
                        AffectedPath: s.ConfigPath,
                        Title: rule.Title, Explanation: rule.Explanation, Recommendation: rule.Recommendation);
                break;

            case "extension":
                foreach (var e in facts.Extensions.Where(e => MatchesExtension(e, rule.Match)))
                    yield return new Finding(rule.Id, severity, e.AppId,
                        ExtensionId: e.ExtensionId,
                        Title: rule.Title, Explanation: rule.Explanation, Recommendation: rule.Recommendation);
                break;

            case "auth_file":
                foreach (var a in facts.AuthFiles.Where(a => MatchesAuth(a, rule.Match)))
                    yield return new Finding(rule.Id, severity, a.AppId,
                        AffectedPath: a.FilePath,
                        Title: rule.Title, Explanation: rule.Explanation, Recommendation: rule.Recommendation);
                break;
        }
    }

    // MARK: - Predicate helpers

    private static bool MatchesServer(McpServerFact s, PackRuleMatch m)
    {
        if (m.CommandEquals is not null && s.Command != m.CommandEquals) return false;
        if (m.CommandContains is not null && !s.Command.Contains(m.CommandContains)) return false;
        if (m.NameContains is not null && !s.Name.Contains(m.NameContains)) return false;
        if (m.ArgsContain is not null && !s.Args.Any(a => a.Contains(m.ArgsContain))) return false;
        if (m.App is not null && s.AppId != m.App) return false;
        return true;
    }

    private static bool MatchesSetting(SettingFact s, PackRuleMatch m)
    {
        if (m.Key is not null && s.Key != m.Key) return false;
        if (m.ValueEquals is not null && s.StringValue != m.ValueEquals) return false;
        if (m.App is not null && s.AppId != m.App) return false;
        return true;
    }

    private static bool MatchesExtension(ExtensionFact e, PackRuleMatch m)
    {
        if (m.ExtensionIdContains is not null && !e.ExtensionId.Contains(m.ExtensionIdContains)) return false;
        if (m.App is not null && e.AppId != m.App) return false;
        return true;
    }

    private static bool MatchesAuth(AuthFileFact a, PackRuleMatch m)
    {
        if (m.App is not null && a.AppId != m.App) return false;
        return true;
    }
}
