public struct AESMCP009: Rule {
    public let id = "AES-MCP-009"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            guard AiThreatPatterns.isRemoteInstallScript(command: server.command, args: server.args) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
