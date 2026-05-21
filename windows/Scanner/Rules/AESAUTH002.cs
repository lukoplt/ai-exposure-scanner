using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESAUTH002 : IRule
{
    public string Id => "AES-AUTH-002";
    public Severity Severity => Severity.Critical;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts) =>
        facts.McpServers
            .Where(server => !server.Disabled)
            .SelectMany(server => server.Args
                .Where(SecretPatterns.ContainsSecret)
                .Select(value => this.Finding(
                    server.AppId,
                    server.Name,
                    affectedPath: server.ConfigPath,
                    maskedValue: SecretPatterns.Masked(value)
                )))
            .ToArray();
}
