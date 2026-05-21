public struct CursorDetector: Detector {
    public let id = "cursor"
    public let displayName = "Cursor"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        let mcpPath = homePath(fs, ".cursor/mcp.json")
        let settingsPath = homePath(fs, "Library/Application Support/Cursor/User/settings.json")
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: ["/Applications/Cursor.app", homePath(fs, "Applications/Cursor.app")]
        )

        if let text = try readIfPresent(fs, path: mcpPath) {
            facts.mcpServers.append(contentsOf: try FactParsers.parseMcpServersJson(text, appId: id, configPath: mcpPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: mcpPath)
        }

        if let text = try readIfPresent(fs, path: settingsPath) {
            facts.settings.append(try FactParsers.parseCursorPrivacyMode(text, appId: id, configPath: settingsPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: settingsPath)
        }

        return facts
    }
}
