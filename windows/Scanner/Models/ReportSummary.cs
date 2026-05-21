namespace AIExposureScanner.Scanner.Models;

public enum OverallStatus
{
    Critical,
    High,
    Medium,
    Low,
    Clean
}

public static class OverallStatusExtensions
{
    public static string ToJsonValue(this OverallStatus status) =>
        status switch
        {
            OverallStatus.Critical => "critical",
            OverallStatus.High => "high",
            OverallStatus.Medium => "medium",
            OverallStatus.Low => "low",
            OverallStatus.Clean => "clean",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
}

public sealed record ReportSummary(
    int ToolsFound,
    int McpServersFound,
    int Critical,
    int High,
    int Medium,
    int Low,
    OverallStatus Status
)
{
    public static ReportSummary FromScanResult(ScanResult scanResult)
    {
        var appsWithFacts = scanResult.Facts.McpServers.Select(fact => fact.AppId)
            .Concat(scanResult.Facts.Settings.Select(fact => fact.AppId))
            .Concat(scanResult.Facts.AuthFiles.Select(fact => fact.AppId))
            .Concat(scanResult.Facts.Extensions.Select(fact => fact.AppId))
            .Concat(scanResult.Facts.ConfigFiles.Select(fact => fact.AppId))
            .Concat(scanResult.Facts.AppInstallations.Where(fact => fact.Installed).Select(fact => fact.AppId))
            .Distinct(StringComparer.Ordinal)
            .Count();

        var critical = scanResult.Findings.Count(finding => finding.Severity == Severity.Critical);
        var high = scanResult.Findings.Count(finding => finding.Severity == Severity.High);
        var medium = scanResult.Findings.Count(finding => finding.Severity == Severity.Medium);
        var low = scanResult.Findings.Count(finding => finding.Severity == Severity.Low);

        return new ReportSummary(
            appsWithFacts,
            scanResult.Facts.McpServers.Count,
            critical,
            high,
            medium,
            low,
            StatusFromCounts(critical, high, medium, low)
        );
    }

    private static OverallStatus StatusFromCounts(int critical, int high, int medium, int low)
    {
        if (critical > 0)
        {
            return OverallStatus.Critical;
        }
        if (high > 0)
        {
            return OverallStatus.High;
        }
        if (medium > 0)
        {
            return OverallStatus.Medium;
        }
        if (low > 0)
        {
            return OverallStatus.Low;
        }
        return OverallStatus.Clean;
    }
}
