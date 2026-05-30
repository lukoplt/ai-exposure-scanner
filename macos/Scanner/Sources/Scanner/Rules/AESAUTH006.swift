public struct AESAUTH006: Rule {
    public let id = "AES-AUTH-006"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        var findings: [Finding] = []

        for server in facts.mcpServers where !server.disabled {
            for (key, value) in server.env where AiThreatPatterns.looksLikeSecretEnvKey(key) {
                // Skip indirect references and values already reported by other rules
                // (provider keys by AES-AUTH-001, cloud creds by AES-AUTH-004,
                // database URLs by AES-AUTH-005).
                guard AiThreatPatterns.isLiteralSecretValue(value) else { continue }
                guard !SecretPatterns.containsSecret(value) else { continue }
                guard !AiThreatPatterns.isCloudCredentialEnv(key: key, value: value) else { continue }
                guard !AiThreatPatterns.isConnectionStringWithCredentials(value) else { continue }

                findings.append(
                    finding(
                        app: server.appId,
                        serverName: server.name,
                        affectedPath: server.configPath,
                        maskedValue: SecretPatterns.masked(value)
                    )
                )
            }
        }

        return findings
    }
}
