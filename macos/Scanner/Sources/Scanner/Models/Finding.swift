public struct Finding: Codable, Equatable, Hashable, Sendable {
    public let ruleId: String
    public let severity: Severity
    public let app: String
    public let serverName: String?
    public let extensionId: String?
    public let affectedPath: String?
    public let maskedValue: String?
    public let title: String
    public let explanation: String
    public let recommendation: String
    public let escalationReason: String?

    public init(
        ruleId: String,
        severity: Severity,
        app: String,
        serverName: String? = nil,
        extensionId: String? = nil,
        affectedPath: String? = nil,
        maskedValue: String? = nil,
        title: String,
        explanation: String,
        recommendation: String,
        escalationReason: String? = nil
    ) {
        self.ruleId = ruleId
        self.severity = severity
        self.app = app
        self.serverName = serverName
        self.extensionId = extensionId
        self.affectedPath = affectedPath
        self.maskedValue = maskedValue
        self.title = title
        self.explanation = explanation
        self.recommendation = recommendation
        self.escalationReason = escalationReason
    }
}
