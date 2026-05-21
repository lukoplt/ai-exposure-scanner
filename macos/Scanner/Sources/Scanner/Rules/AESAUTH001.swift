public struct AESAUTH001: Rule {
    public let id = "AES-AUTH-001"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        var findings: [Finding] = []

        for server in facts.mcpServers where !server.disabled {
            for value in server.env.values where SecretPatterns.containsSecret(value) {
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

        for setting in facts.settings where setting.key == "apiKey" {
            if let value = setting.stringValue, SecretPatterns.containsSecret(value) {
                findings.append(
                    finding(
                        app: setting.appId,
                        affectedPath: setting.configPath,
                        maskedValue: SecretPatterns.masked(value)
                    )
                )
            }
        }

        return findings
    }
}
