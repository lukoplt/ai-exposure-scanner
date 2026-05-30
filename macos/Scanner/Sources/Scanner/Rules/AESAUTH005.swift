public struct AESAUTH005: Rule {
    public let id = "AES-AUTH-005"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let match = server.searchableValues.first { value in
                AiThreatPatterns.isConnectionStringWithCredentials(value)
            }
            guard let match else {
                return nil
            }
            return finding(
                app: server.appId,
                serverName: server.name,
                affectedPath: server.configPath,
                maskedValue: SecretPatterns.masked(match)
            )
        }
    }
}
