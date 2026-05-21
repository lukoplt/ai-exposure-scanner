public enum EscalationScope: Sendable {
    case perServer
    case global
}

public struct EscalationRule: Sendable {
    public let requiresRuleIds: Set<String>
    public let scope: EscalationScope
    public let escalateTo: Severity
    public let reason: String
    /// For global scope: minimum number of distinct serverNames that must carry
    /// at least one finding from requiresRuleIds. Default 1.
    public let minimumDistinctServers: Int

    public init(
        requiresRuleIds: Set<String>,
        scope: EscalationScope,
        escalateTo: Severity,
        reason: String,
        minimumDistinctServers: Int = 1
    ) {
        self.requiresRuleIds = requiresRuleIds
        self.scope = scope
        self.escalateTo = escalateTo
        self.reason = reason
        self.minimumDistinctServers = minimumDistinctServers
    }
}
