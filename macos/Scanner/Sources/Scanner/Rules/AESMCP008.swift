public struct AESMCP008: Rule {
    public let id = "AES-MCP-008"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let overridden = server.env.contains { key, value in
                AiThreatPatterns.isModelBaseUrlOverride(key: key, value: value)
            }
            guard overridden else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
