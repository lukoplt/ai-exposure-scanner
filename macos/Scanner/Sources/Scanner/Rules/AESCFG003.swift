public struct AESCFG003: Rule {
    public let id = "AES-CFG-003"
    public let severity: Severity = .low

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let description = server.description?.trimmingCharacters(in: .whitespacesAndNewlines)
            guard description?.isEmpty != false else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
