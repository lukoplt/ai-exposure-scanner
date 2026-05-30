public struct RuleEvaluator: Sendable {
    public let rules: [any Rule]

    public init(rules: [any Rule] = RuleEvaluator.defaultRules) {
        self.rules = rules
    }

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        let findings = rules.flatMap { $0.evaluate(facts) }
        return Array(Set(findings)).sorted(by: FindingSort.areInIncreasingOrder)
    }

    public static let defaultRules: [any Rule] = [
        AESMCP001(),
        AESMCP002(),
        AESAUTH001(),
        AESAUTH002(),
        AESMCP003(),
        AESMCP004(),
        AESMCP005(),
        AESEXT001(),
        AESCFG001(),
        AESMCP006(),
        AESMCP007(),
        AESAUTH003(),
        AESCFG002(),
        AESCFG003(),
        AESCFG004(),
        AESMCP008(),
        AESMCP009(),
        AESMCP010(),
        AESMCP011(),
        AESMCP012(),
        AESMCP013(),
        AESAUTH004(),
        AESAUTH005(),
        AESAUTH006()
    ]
}

enum FindingSort {
    static func areInIncreasingOrder(_ lhs: Finding, _ rhs: Finding) -> Bool {
        let left = sortKey(lhs)
        let right = sortKey(rhs)
        return left < right
    }

    private static func sortKey(_ finding: Finding) -> String {
        [
            String(finding.severity.sortRank),
            finding.app,
            finding.serverName ?? "",
            finding.extensionId ?? "",
            finding.ruleId
        ].joined(separator: "|")
    }
}
