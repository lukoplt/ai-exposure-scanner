using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESAUTH004 : IRule
{
    public string Id => "AES-AUTH-004";
    public Severity Severity => Severity.Critical;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts)
    {
        var findings = new List<Finding>();
        foreach (var server in facts.McpServers.Where(server => !server.Disabled))
        {
            var match = server.Env.FirstOrDefault(kv => AiThreatPatterns.IsCloudCredentialEnv(kv.Key, kv.Value));
            if (match.Key is null)
            {
                continue;
            }
            findings.Add(this.Finding(
                server.AppId,
                server.Name,
                affectedPath: server.ConfigPath,
                maskedValue: SecretPatterns.Masked(match.Value)
            ));
        }
        return findings;
    }
}
