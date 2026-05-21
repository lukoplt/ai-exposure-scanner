public struct VSCodeDetector: Detector {
    public let id = "vscode"
    public let displayName = "VS Code"

    public init() {}

    public func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts {
        var facts = ScanFacts()
        appendAppInstallationFact(
            &facts,
            fs: fs,
            appId: id,
            candidatePaths: ["/Applications/Visual Studio Code.app", homePath(fs, "Applications/Visual Studio Code.app")]
        )

        let extensionsPath = homePath(fs, ".vscode/extensions")
        if fs.directoryExists(extensionsPath) {
            facts.extensions.append(contentsOf: try parseExtensions(fs.listDirectoryNames(extensionsPath)))
        }

        let continuePath = homePath(fs, ".continue/config.json")
        if let text = try readIfPresent(fs, path: continuePath) {
            facts.mcpServers.append(contentsOf: try FactParsers.parseMcpServersJson(text, appId: id, configPath: continuePath))
        }

        let storageRoot = homePath(fs, "Library/Application Support/Code/User/globalStorage")
        for extensionDir in ["saoudrizwan.claude-dev", "rooveterinaryinc.roo-cline"] {
            let root = storageRoot + "/" + extensionDir
            guard fs.directoryExists(root) else {
                continue
            }
            let candidates = try fs.listFilesRecursively(root, maxDepth: 3)
                .filter { path in
                    let lower = path.lowercased()
                    return lower.hasSuffix(".json") && lower.contains("mcp")
                }
            for path in candidates {
                if let text = try readIfPresent(fs, path: path) {
                    facts.mcpServers.append(contentsOf: try FactParsers.parseMcpServersJson(text, appId: id, configPath: path))
                }
            }
        }

        return facts
    }

    private func parseExtensions(_ folders: [String]) -> [ExtensionFact] {
        folders.compactMap { folder in
            if folder.hasPrefix("saoudrizwan.claude-dev-") {
                return ExtensionFact(appId: id, extensionId: "saoudrizwan.claude-dev", extensionName: "Cline", hasTerminalAccess: true)
            }
            if folder.hasPrefix("rooveterinaryinc.roo-cline-") {
                return ExtensionFact(appId: id, extensionId: "rooveterinaryinc.roo-cline", extensionName: "Roo", hasTerminalAccess: true)
            }
            if folder.hasPrefix("continue.continue-") {
                return ExtensionFact(appId: id, extensionId: "continue.continue", extensionName: "Continue", hasTerminalAccess: true)
            }
            if folder.hasPrefix("github.copilot-") {
                return ExtensionFact(appId: id, extensionId: "github.copilot", extensionName: "GitHub Copilot", hasTerminalAccess: false)
            }
            return nil
        }
    }
}
