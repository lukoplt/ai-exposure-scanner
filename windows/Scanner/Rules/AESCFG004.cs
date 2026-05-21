using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESCFG004 : IRule
{
    public string Id => "AES-CFG-004";
    public Severity Severity => Severity.Low;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.ConfigFiles
            .Where(configFile => configFile.WorldReadable)
            .Select(configFile => this.Finding(configFile.AppId, affectedPath: configFile.Path))
            .ToArray();
}
