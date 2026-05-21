using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AIExposureScanner.Scanner.RulePacks;

public abstract record RulePackLoadResult
{
    public sealed record Valid(RulePack Pack) : RulePackLoadResult;
    public sealed record Invalid(string Error) : RulePackLoadResult;
}

public static class RulePackLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static RulePackLoadResult Load(string yaml)
    {
        try
        {
            return Parse(yaml);
        }
        catch (Exception ex)
        {
            return new RulePackLoadResult.Invalid($"YAML parse error: {ex.Message}");
        }
    }

    private static RulePackLoadResult Parse(string yaml)
    {
        var dto = Deserializer.Deserialize<RulePackDto?>(yaml);
        if (dto is null)
            return new RulePackLoadResult.Invalid("Empty YAML document");

        if (string.IsNullOrWhiteSpace(dto.Version))
            return new RulePackLoadResult.Invalid("Missing required field: version");
        if (string.IsNullOrWhiteSpace(dto.Id))
            return new RulePackLoadResult.Invalid("Missing required field: id");
        if (dto.Id.StartsWith("AES-", StringComparison.OrdinalIgnoreCase))
            return new RulePackLoadResult.Invalid("Rule pack id must not start with 'AES-' (reserved for built-in rules)");
        if (string.IsNullOrWhiteSpace(dto.Name))
            return new RulePackLoadResult.Invalid("Missing required field: name");

        var rules = new List<PackRule>();
        foreach (var (i, r) in (dto.Rules ?? []).Select((r, i) => (i, r)))
        {
            var err = ValidateRule(r, $"rules[{i}]");
            if (err is not null) return new RulePackLoadResult.Invalid(err);
            rules.Add(new PackRule(r.Id!, r.Severity!, r.Title ?? "", r.Explanation ?? "", r.Recommendation ?? "",
                new PackRuleMatch(r.Match!.Fact!,
                    r.Match.CommandEquals, r.Match.CommandContains,
                    r.Match.NameContains, r.Match.ArgsContain,
                    r.Match.App, r.Match.Key,
                    r.Match.ValueEquals, r.Match.ExtensionIdContains)));
        }

        var overrides = new List<PackOverride>();
        foreach (var (i, o) in (dto.Overrides ?? []).Select((o, i) => (i, o)))
        {
            var err = ValidateOverride(o, $"overrides[{i}]");
            if (err is not null) return new RulePackLoadResult.Invalid(err);
            overrides.Add(new PackOverride(o.Id!, o.Severity!));
        }

        var escalations = new List<PackEscalation>();
        foreach (var (i, e) in (dto.Escalations ?? []).Select((e, i) => (i, e)))
        {
            var err = ValidateEscalation(e, $"escalations[{i}]");
            if (err is not null) return new RulePackLoadResult.Invalid(err);
            escalations.Add(new PackEscalation(e.Requires!, e.Scope!, e.EscalateTo!, e.Reason ?? ""));
        }

        return new RulePackLoadResult.Valid(
            new RulePack(dto.Version!, dto.Id!, dto.Name!, rules, overrides, escalations));
    }

    // MARK: - Validation helpers

    private static readonly HashSet<string> ValidSeverities =
        new(["critical", "high", "medium", "low"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValidFacts =
        new(["mcp_server", "setting", "extension", "auth_file"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValidScopes =
        new(["per_server", "global"], StringComparer.OrdinalIgnoreCase);

    private static string? ValidateRule(RuleDto r, string prefix)
    {
        if (string.IsNullOrWhiteSpace(r.Id)) return $"{prefix}: missing required field 'id'";
        if (r.Id.StartsWith("AES-", StringComparison.OrdinalIgnoreCase))
            return $"{prefix}: rule id '{r.Id}' must not start with 'AES-'";
        if (!ValidSeverities.Contains(r.Severity ?? ""))
            return $"{prefix}: missing or invalid 'severity' (use: critical, high, medium, low)";
        if (r.Match is null) return $"{prefix}: missing required field 'match'";
        if (!ValidFacts.Contains(r.Match.Fact ?? ""))
            return $"{prefix}.match: missing or invalid 'fact' (use: mcp_server, setting, extension, auth_file)";
        return null;
    }

    private static string? ValidateOverride(OverrideDto o, string prefix)
    {
        if (string.IsNullOrWhiteSpace(o.Id)) return $"{prefix}: missing required field 'id'";
        if (!ValidSeverities.Contains(o.Severity ?? ""))
            return $"{prefix}: missing or invalid 'severity'";
        return null;
    }

    private static string? ValidateEscalation(EscalationDto e, string prefix)
    {
        if (e.Requires is null || e.Requires.Count == 0)
            return $"{prefix}: missing or empty 'requires' (list of rule IDs)";
        if (!ValidScopes.Contains(e.Scope ?? ""))
            return $"{prefix}: missing or invalid 'scope' (use: per_server, global)";
        if (!ValidSeverities.Contains(e.EscalateTo ?? ""))
            return $"{prefix}: missing or invalid 'escalate_to' (use: critical, high, medium, low)";
        return null;
    }

    // MARK: - DTO types (YAML deserialization targets)

    private sealed class RulePackDto
    {
        public string? Version { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<RuleDto>? Rules { get; set; }
        public List<OverrideDto>? Overrides { get; set; }
        public List<EscalationDto>? Escalations { get; set; }
    }

    private sealed class RuleDto
    {
        public string? Id { get; set; }
        public string? Severity { get; set; }
        public string? Title { get; set; }
        public string? Explanation { get; set; }
        public string? Recommendation { get; set; }
        public MatchDto? Match { get; set; }
    }

    private sealed class MatchDto
    {
        public string? Fact { get; set; }
        public string? CommandEquals { get; set; }
        public string? CommandContains { get; set; }
        public string? NameContains { get; set; }
        public string? ArgsContain { get; set; }
        public string? App { get; set; }
        public string? Key { get; set; }
        public string? ValueEquals { get; set; }
        public string? ExtensionIdContains { get; set; }
    }

    private sealed class OverrideDto
    {
        public string? Id { get; set; }
        public string? Severity { get; set; }
    }

    private sealed class EscalationDto
    {
        public List<string>? Requires { get; set; }
        public string? Scope { get; set; }
        public string? EscalateTo { get; set; }
        public string? Reason { get; set; }
    }
}
