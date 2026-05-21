public struct AESAUTH002: Rule {
    public let id = "AES-AUTH-002"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        var findings: [Finding] = []

        for server in facts.mcpServers where !server.disabled {
            for value in server.args where SecretPatterns.containsSecret(value) {
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
