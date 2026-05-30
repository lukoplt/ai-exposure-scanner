public struct AESMCP011: Rule {
    public let id = "AES-MCP-011"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            let touchesCredentialDir = server.searchableValues.contains { value in
                AiThreatPatterns.containsSensitiveCredentialPath(value)
            }
            guard touchesCredentialDir else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
