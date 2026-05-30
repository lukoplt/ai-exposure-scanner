public struct AESMCP012: Rule {
    public let id = "AES-MCP-012"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let reachesDockerDaemon = server.searchableValues.contains { value in
                AiThreatPatterns.grantsDockerDaemonAccess(value)
            }
            guard reachesDockerDaemon else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
