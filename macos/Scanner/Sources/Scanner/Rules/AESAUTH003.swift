public struct AESAUTH003: Rule {
    public let id = "AES-AUTH-003"
    public let severity: Severity = .medium

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        facts.authFiles.map { authFile in
            finding(app: authFile.appId, affectedPath: authFile.filePath)
        }
    }
}
