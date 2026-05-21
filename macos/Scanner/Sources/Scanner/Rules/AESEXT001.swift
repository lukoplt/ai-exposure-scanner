public struct AESEXT001: Rule {
    public let id = "AES-EXT-001"
    public let severity: Severity = .high

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.extensions.compactMap { ext in
            guard ext.hasTerminalAccess else {
                return nil
            }
            return finding(app: ext.appId, extensionId: ext.extensionId)
        }
    }
}
