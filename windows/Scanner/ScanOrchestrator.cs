using AIExposureScanner.Scanner.Detectors;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.RulePacks;

namespace AIExposureScanner.Scanner;

public sealed class ScanOrchestrator
{
    public ScanOrchestrator(IEnumerable<IDetector>? detectors = null, RuleEvaluator? evaluator = null)
    {
        Detectors = (detectors ?? DefaultDetectors).ToArray();
        Evaluator = evaluator ?? new RuleEvaluator();
    }

    public IReadOnlyList<IDetector> Detectors { get; }
    public RuleEvaluator Evaluator { get; }

    public async Task<ScanResult> ScanAsync(
        IFilesystemFacade fs,
        CancellationToken ct = default,
        IReadOnlyList<RulePack>? packs = null)
    {
        packs ??= [];

        var tasks = Detectors.Select(async detector =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                return await Task.Run(() => detector.CollectFacts(fs), cts.Token);
            }
            catch (OperationCanceledException)
            {
                return new ScanFacts();
            }
            catch (Exception)
            {
                return new ScanFacts();
            }
        });

        var allFacts = await Task.WhenAll(tasks);
        var merged = new ScanFacts();
        foreach (var f in allFacts) merged.Append(f);

        var builtInFindings = Evaluator.Evaluate(merged);
        var allFindings = new RulePackEvaluator().Evaluate(merged, packs, builtInFindings);
        var escalationRules = EscalationRules.BuiltIn
            .Concat(packs.SelectMany(p => p.EscalationRules))
            .ToList();
        var escalated = new EscalationEvaluator().Evaluate(allFindings, escalationRules);

        return new ScanResult(merged, escalated);
    }

    /// <summary>Synchronous shim for tests and legacy callers.</summary>
    public ScanResult Scan(IFilesystemFacade fs) =>
        ScanAsync(fs).GetAwaiter().GetResult();

    public static IReadOnlyList<IDetector> DefaultDetectors { get; } =
    [
        new ClaudeDesktopDetector(),
        new CursorDetector(),
        new WindsurfDetector(),
        new VSCodeDetector(),
        new CodexCliDetector(),
        new GeminiCliDetector(),
        new ClaudeCliDetector()
    ];
}
