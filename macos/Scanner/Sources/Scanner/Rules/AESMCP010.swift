public struct AESMCP010: Rule {
    public let id = "AES-MCP-010"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let usesPlaintextHttp = server.searchableValues.contains { value in
                AiThreatPatterns.isPlaintextHttpEndpoint(value)
            }
            guard usesPlaintextHttp else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
