using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESCFG001 : IRule
{
    public string Id => "AES-CFG-001";
    public Severity Severity => Severity.High;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.Settings
            .Where(setting => setting.AppId == "cursor" && setting.Key == "cursor.privacyMode")
            .Where(setting => setting.BoolValue == false)
            .Select(setting => this.Finding(setting.AppId, affectedPath: setting.ConfigPath))
            .ToArray();
}
