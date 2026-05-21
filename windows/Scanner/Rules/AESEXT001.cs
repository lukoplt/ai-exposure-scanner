using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESEXT001 : IRule
{
    public string Id => "AES-EXT-001";
    public Severity Severity => Severity.High;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.Extensions
            .Where(extension => extension.HasTerminalAccess)
            .Select(extension => this.Finding(extension.AppId, extensionId: extension.ExtensionId))
            .ToArray();
}
