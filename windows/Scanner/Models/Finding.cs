namespace AIExposureScanner.Scanner.Models;

public sealed record Finding(
    string RuleId,
    Severity Severity,
    string App,
    string? ServerName = null,
    string? ExtensionId = null,
    string? AffectedPath = null,
    string? MaskedValue = null,
    string Title = "",
    string Explanation = "",
    string Recommendation = "",
    string? EscalationReason = null
);
