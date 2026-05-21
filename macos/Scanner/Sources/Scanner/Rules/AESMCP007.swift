public struct AESMCP007: Rule {
    public let id = "AES-MCP-007"
    public let severity: Severity = .medium

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }
            guard PackageVersion.isPackageRunner(server.commandName) else {
                return nil
            }
            guard !PackageVersion.hasPinnedVersion(args: server.args) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
