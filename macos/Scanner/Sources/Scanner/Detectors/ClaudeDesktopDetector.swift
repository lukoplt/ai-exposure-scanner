public struct ClaudeDesktopDetector: Detector {
    public let id = "claude-desktop"
    public let displayName = "Claude Desktop"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        let configPath = homePath(fs, "Library/Application Support/Claude/claude_desktop_config.json")
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: ["/Applications/Claude.app", homePath(fs, "Applications/Claude.app")]
        )

        if let text = try readIfPresent(fs, path: configPath) {
            facts.mcpServers.append(contentsOf: try FactParsers.parseMcpServersJson(text, appId: id, configPath: configPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: configPath)
        }

        return facts
    }
}
