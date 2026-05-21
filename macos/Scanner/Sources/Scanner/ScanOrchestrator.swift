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

    public func scan(fs: any FilesystemFacade) throws -> ScanResult {
        var facts = ScanFacts()
        for detector in detectors {
            facts.append(try detector.collectFacts(fs: fs))
        }

        return ScanResult(facts: facts, findings: evaluator.evaluate(facts))
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
