using AIExposureScanner.Scanner.Detectors;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;

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

    public async Task<ScanResult> ScanAsync(IFilesystemFacade fs, CancellationToken ct = default)
    {
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

        return new ScanResult(merged, Evaluator.Evaluate(merged));
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
        new GeminiCliDetector()
    ];
}
