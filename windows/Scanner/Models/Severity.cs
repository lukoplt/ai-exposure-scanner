namespace AIExposureScanner.Scanner.Models;

public enum Severity
{
    Critical,
    High,
    Medium,
    Low
}

public static class SeverityExtensions
{
    public static string ToJsonValue(this Severity severity) =>
        severity switch
        {
            Severity.Critical => "critical",
            Severity.High => "high",
            Severity.Medium => "medium",
            Severity.Low => "low",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };

    public static int SortRank(this Severity severity) =>
        severity switch
        {
            Severity.Critical => 0,
            Severity.High => 1,
            Severity.Medium => 2,
            Severity.Low => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null)
        };
}
