public struct GeminiCliDetector: Detector {
    public let id = "gemini-cli"
    public let displayName = "Gemini CLI"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        let settingsPath = homePath(fs, ".gemini/settings.json")
        let credentialsPath = homePath(fs, ".gemini/credentials")
        let oauthPath = homePath(fs, ".gemini/oauth_credentials.json")
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: [homePath(fs, ".gemini")]
        )

        if let text = try readIfPresent(fs, path: settingsPath) {
            facts.append(try FactParsers.parseGeminiSettings(text, appId: id, configPath: settingsPath))
            appendConfigFileFact(&facts, fs: fs, appId: id, path: settingsPath)
        }

        if fs.fileExists(credentialsPath) {
            facts.authFiles.append(AuthFileFact(appId: id, filePath: credentialsPath))
        }
        if fs.fileExists(oauthPath) {
            facts.authFiles.append(AuthFileFact(appId: id, filePath: oauthPath))
        }

        return facts
    }
}
