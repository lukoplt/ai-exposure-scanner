public struct WindsurfDetector: Detector {
    public let id = "windsurf"
    public let displayName = "Windsurf"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        let mcpPath = homePath(fs, ".codeium/windsurf/mcp_config.json")
        let settingsPath = homePath(fs, "Library/Application Support/Windsurf/User/settings.json")
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: ["/Applications/Windsurf.app", homePath(fs, "Applications/Windsurf.app")]
        )

        if let text = try readIfPresent(fs, path: mcpPath) {
            facts.mcpServers.append(contentsOf: try FactParsers.parseMcpServersJson(text, appId: id, configPath: mcpPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: mcpPath)
        }

        appendConfigFileFact(&facts, fs: fs, appId: id, path: settingsPath)
        return facts
    }
}
