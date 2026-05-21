public struct ClaudeCliDetector: Detector {
    public let id = "claude-cli"
    public let displayName = "Claude CLI"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        // Global config: ~/.claude.json (macOS/Linux) or %USERPROFILE%\.claude.json (Windows)
        let configPath = homePath(fs, ".claude.json")
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: [homePath(fs, ".claude")]
        )

        if let text = try readIfPresent(fs, path: configPath) {
            facts.mcpServers.append(contentsOf: try FactParsers.parseMcpServersJson(text, appId: id, configPath: configPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: configPath)
        }

        return facts
    }
}
