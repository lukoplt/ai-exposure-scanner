using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

public sealed class AESAUTH006 : IRule
{
    public string Id => "AES-AUTH-006";
    public Severity Severity => Severity.High;

    public IReadOnlyList<Finding> Evaluate(ScanFacts facts)
    {
        var findings = new List<Finding>();
        foreach (var server in facts.McpServers.Where(server => !server.Disabled))
        {
            foreach (var kv in server.Env)
            {
                if (!AiThreatPatterns.LooksLikeSecretEnvKey(kv.Key))
                {
                    continue;
                }
                // Skip indirect references and values already reported by other rules
                // (provider keys by AES-AUTH-001, cloud creds by AES-AUTH-004,
                // database URLs by AES-AUTH-005).
                if (!AiThreatPatterns.IsLiteralSecretValue(kv.Value))
                {
                    continue;
                }
                if (SecretPatterns.ContainsSecret(kv.Value))
                {
                    continue;
                }
                if (AiThreatPatterns.IsCloudCredentialEnv(kv.Key, kv.Value))
                {
                    continue;
                }
                if (AiThreatPatterns.IsConnectionStringWithCredentials(kv.Value))
                {
                    continue;
                }
                findings.Add(this.Finding(
                    server.AppId,
                    server.Name,
                    affectedPath: server.ConfigPath,
                    maskedValue: SecretPatterns.Masked(kv.Value)
                ));
            }
        }
        return findings;
    }
}
