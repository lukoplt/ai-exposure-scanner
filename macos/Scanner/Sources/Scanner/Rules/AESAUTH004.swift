public struct AESAUTH004: Rule {
    public let id = "AES-AUTH-004"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let match = server.env.first { key, value in
                AiThreatPatterns.isCloudCredentialEnv(key: key, value: value)
            }
            guard let match else {
                return nil
            }
            return finding(
                app: server.appId,
                serverName: server.name,
                affectedPath: server.configPath,
                maskedValue: SecretPatterns.masked(match.value)
            )
        }
    }
}
