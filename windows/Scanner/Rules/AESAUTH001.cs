using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESAUTH001 : IRule
{
    public string Id => "AES-AUTH-001";
    public Severity Severity => Severity.Critical;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts)
    {
        var findings = new List<Finding>();

        foreach (var server in facts.McpServers.Where(server => !server.Disabled))
        {
            findings.AddRange(
                server.Env.Values
                    .Where(SecretPatterns.ContainsSecret)
                    .Select(value => this.Finding(
                        server.AppId,
                        server.Name,
                        affectedPath: server.ConfigPath,
                        maskedValue: SecretPatterns.Masked(value)
                    ))
            );
        }

        findings.AddRange(
            facts.Settings
                .Where(setting => setting.Key == "apiKey")
                .Where(setting => setting.StringValue is not null && SecretPatterns.ContainsSecret(setting.StringValue))
                .Select(setting => this.Finding(
                    setting.AppId,
                    affectedPath: setting.ConfigPath,
                    maskedValue: SecretPatterns.Masked(setting.StringValue!)
                ))
        );

        return findings;
    }
}
