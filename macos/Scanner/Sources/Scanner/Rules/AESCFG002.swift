public struct AESCFG002: Rule {
    public let id = "AES-CFG-002"
    public let severity: Severity = .medium

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        let activeServers = facts.mcpServers.filter { !$0.disabled }
        let grouped = Dictionary(grouping: activeServers, by: { $0.name.lowercased() })
        let duplicates = grouped.values.filter { servers in
            Set(servers.map(\.appId)).count > 1
        }

        return duplicates.flatMap { servers in
            servers.map { server in
                finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
            }
        }
    }
}
