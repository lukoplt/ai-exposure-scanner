public struct ScanResult: Equatable, Sendable {
    public let facts: ScanFacts
    public let findings: [Finding]

    public init(facts: ScanFacts, findings: [Finding]) {
        self.facts = facts
        self.findings = findings
    }
}
