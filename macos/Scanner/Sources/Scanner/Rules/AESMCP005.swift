public struct AESMCP005: Rule {
    public let id = "AES-MCP-005"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        let browserMarkers = [
            "playwright",
            "puppeteer",
            "browser-use",
            "selenium",
            "@playwright/mcp",
            "playwright-mcp"
        ]

        return facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }

            let values = server.searchableValues.map { $0.lowercased() }
            guard values.contains(where: { value in browserMarkers.contains(where: value.contains) }) else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
