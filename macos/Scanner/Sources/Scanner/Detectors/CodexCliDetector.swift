public struct CodexCliDetector: Detector {
    public let id = "codex-cli"
    public let displayName = "Codex CLI"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        let configPath = homePath(fs, ".codex/config.toml")
        let authPath = homePath(fs, ".codex/auth.json")
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: [homePath(fs, ".codex")]
        )

        if let text = try readIfPresent(fs, path: configPath) {
            facts.mcpServers.append(contentsOf: FactParsers.parseCodexToml(text, appId: id, configPath: configPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: configPath)
        }

        if fs.fileExists(authPath) {
            facts.authFiles.append(AuthFileFact(appId: id, filePath: authPath))
        }

        return facts
    }
}
