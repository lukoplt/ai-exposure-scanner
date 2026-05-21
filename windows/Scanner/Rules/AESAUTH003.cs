using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESAUTH003 : IRule
{
    public string Id => "AES-AUTH-003";
    public Severity Severity => Severity.Medium;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.AuthFiles
            .Select(authFile => this.Finding(authFile.AppId, affectedPath: authFile.FilePath))
            .ToArray();
}
