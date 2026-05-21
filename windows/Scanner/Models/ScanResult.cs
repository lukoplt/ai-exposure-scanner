namespace AIExposureScanner.Scanner.Models;

public sealed record ScanResult(ScanFacts Facts, IReadOnlyList<Finding> Findings);
