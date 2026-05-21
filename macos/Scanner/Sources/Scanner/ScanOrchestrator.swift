public struct ScanOrchestrator: Sendable {
    public let detectors: [any Detector]
    public let evaluator: RuleEvaluator

    public init(
        detectors: [any Detector] = ScanOrchestrator.defaultDetectors,
        evaluator: RuleEvaluator = RuleEvaluator()
    ) {
        self.detectors = detectors
        self.evaluator = evaluator
    }

    public func scan(fs: any FilesystemFacade, packs: [RulePack] = []) throws -> ScanResult {
        var facts = ScanFacts()
        for detector in detectors {
            facts.append(try detector.collectFacts(fs: fs))
        }

        let builtInFindings = evaluator.evaluate(facts)
        let allFindings = RulePackEvaluator().evaluate(
            facts: facts, packs: packs, existingFindings: builtInFindings)
        let escalationRules = EscalationRules.builtIn + packs.flatMap(\.escalationRules)
        let escalatedFindings = EscalationEvaluator().evaluate(allFindings, rules: escalationRules)

        return ScanResult(facts: facts, findings: escalatedFindings)
    }

    public static let defaultDetectors: [any Detector] = [
        ClaudeDesktopDetector(),
        CursorDetector(),
        WindsurfDetector(),
        VSCodeDetector(),
        CodexCliDetector(),
        GeminiCliDetector()
    ]
}
