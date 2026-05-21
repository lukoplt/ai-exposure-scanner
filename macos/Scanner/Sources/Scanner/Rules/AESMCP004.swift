public struct AESMCP004: Rule {
    public let id = "AES-MCP-004"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        let networkCommands = ["curl", "wget", "httpie"]

        return facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }

            let command = server.commandName.lowercased()
            let values = server.searchableValues.map { $0.lowercased() }
            let hasNetworkTool = networkCommands.contains(command)
            let hasFetchServer = values.contains { value in
                value.contains("@modelcontextprotocol/server-fetch") ||
                    value.contains("mcp-server-fetch")
            }

            guard hasNetworkTool || hasFetchServer else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
