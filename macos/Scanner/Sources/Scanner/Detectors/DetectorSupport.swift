func homePath(_ fs: any FilesystemFacade, _ suffix: String) -> String {
    if suffix.isEmpty {
        return fs.homeDirectory
    }
    return fs.homeDirectory + "/" + suffix
}

func appendConfigFileFact(_ facts: inout ScanFacts, fs: any FilesystemFacade, appId: String, path: String) {
    if fs.fileExists(path) {
        facts.configFiles.append(
            ConfigFileFact(appId: appId, path: path, worldReadable: fs.isWorldReadableFile(path))
        )
    }
}

func appendAppInstallationFact(
    _ facts: inout ScanFacts,
    fs: any FilesystemFacade,
    appId: String,
    candidatePaths: [String]
) {
    if let evidencePath = candidatePaths.first(where: { fs.directoryExists($0) }) {
        facts.appInstallations.append(AppInstallationFact(appId: appId, installed: true, evidencePath: evidencePath))
    } else {
        facts.appInstallations.append(AppInstallationFact(appId: appId, installed: false))
    }
}

func readIfPresent(_ fs: any FilesystemFacade, path: String) throws -> String? {
    guard fs.fileExists(path) else {
        return nil
    }
    return try fs.readTextFile(path, maxBytes: 10 * 1024 * 1024)
}
