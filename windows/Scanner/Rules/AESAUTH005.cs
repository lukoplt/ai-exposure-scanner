using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESAUTH005 : IRule
{
    public string Id => "AES-AUTH-005";
    public Severity Severity => Severity.Critical;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts)
    {
        var findings = new List<Finding>();
        foreach (var server in facts.McpServers.Where(server => !server.Disabled))
        {
            var match = server.SearchableValues()
                .FirstOrDefault(AiThreatPatterns.IsConnectionStringWithCredentials);
            if (match is null)
            {
                continue;
            }
            findings.Add(this.Finding(
                server.AppId,
                server.Name,
                affectedPath: server.ConfigPath,
                maskedValue: SecretPatterns.Masked(match)
            ));
        }
        return findings;
    }
}
