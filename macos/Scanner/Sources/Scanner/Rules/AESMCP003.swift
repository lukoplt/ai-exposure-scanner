public struct AESMCP003: Rule {
    public let id = "AES-MCP-003"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            guard server.searchableValues.contains(where: PathRisk.isBroadFilesystemPath) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
