namespace AIExposureScanner.Scanner.Models;

public sealed record Finding(
    string RuleId,
    Severity Severity,
    string App,
    string? ServerName,
    string? ExtensionId,
    string? AffectedPath,
    string? MaskedValue,
    string Title,
    string Explanation,
    string Recommendation
);
