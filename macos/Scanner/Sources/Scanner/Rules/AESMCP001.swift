public struct AESMCP001: Rule {
    public let id = "AES-MCP-001"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            guard server.searchableValues.contains(where: PathRisk.isHomeRoot) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
