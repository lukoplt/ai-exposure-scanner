public struct AESMCP006: Rule {
    public let id = "AES-MCP-006"
    public let severity: Severity = .medium

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        let uninstalledAppIds = Set(facts.appInstallations.filter { !$0.installed }.map(\.appId))
        let findings: [Finding] = facts.mcpServers.compactMap { server in
            guard server.disabled || uninstalledAppIds.contains(server.appId) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
        return findings
    }
}
