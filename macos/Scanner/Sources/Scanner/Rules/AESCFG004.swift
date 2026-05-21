public struct AESCFG004: Rule {
    public let id = "AES-CFG-004"
    public let severity: Severity = .low

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.configFiles.compactMap { configFile in
            guard configFile.worldReadable else {
                return nil
            }
            return finding(app: configFile.appId, affectedPath: configFile.path)
        }
    }
}
