public protocol Rule: Sendable {
    var id: String { get }
    var severity: Severity { get }

    func evaluate(_ facts: ScanFacts) -> [Finding]
}
