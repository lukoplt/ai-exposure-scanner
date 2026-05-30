public struct AESMCP013: Rule {
    public let id = "AES-MCP-013"
    public let severity: Severity = .medium

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            guard AiThreatPatterns.isMessagingServer(values: server.searchableValues) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
