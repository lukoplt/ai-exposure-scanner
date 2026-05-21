using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Rules;

namespace AIExposureScanner.Scanner;

public sealed class RuleEvaluator
{
    public RuleEvaluator(IEnumerable<IRule>? rules = null)
    {
        Rules = (rules ?? DefaultRules).ToArray();
    }

    public IReadOnlyList<IRule> Rules { get; }

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        Rules
            .SelectMany(rule => rule.Evaluate(facts))
            .Distinct()
            .OrderBy(finding => finding.Severity.SortRank())
            .ThenBy(finding => finding.App, StringComparer.Ordinal)
            .ThenBy(finding => finding.ServerName ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(finding => finding.ExtensionId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(finding => finding.RuleId, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<IRule> DefaultRules { get; } =
    [
        new AESMCP001(),
        new AESMCP002(),
        new AESAUTH001(),
        new AESAUTH002(),
        new AESMCP003(),
        new AESMCP004(),
        new AESMCP005(),
        new AESEXT001(),
        new AESCFG001(),
        new AESMCP006(),
        new AESMCP007(),
        new AESAUTH003(),
        new AESCFG002(),
        new AESCFG003(),
        new AESCFG004()
    ];
}
