public struct AESCFG001: Rule {
    public let id = "AES-CFG-001"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.settings.compactMap { setting in
            guard setting.appId == "cursor", setting.key == "cursor.privacyMode" else {
                return nil
            }
            guard setting.boolValue == false else {
                return nil
            }
            return finding(app: setting.appId, affectedPath: setting.configPath)
        }
    }
}
