// Full model — populated in Task 9
public struct RulePack: Sendable {
    public let version: String
    public let id: String
    public let name: String
    public let rules: [PackRule]
    public let overrides: [PackOverride]
    public let escalations: [PackEscalation]

    public var escalationRules: [EscalationRule] {
        escalations.compactMap { pe in
            guard let scope = EscalationScope(yamlValue: pe.scope),
                  let escalateTo = Severity(rawValue: pe.escalateTo) else { return nil }
            return EscalationRule(
                requiresRuleIds: Set(pe.requires),
                scope: scope,
                escalateTo: escalateTo,
                reason: pe.reason
            )
        }
    }
}

public struct PackRule: Sendable {
    public let id: String
    public let severity: Severity
    public let title: String
    public let explanation: String
    public let recommendation: String
    public let match: PackRuleMatch
}

public struct PackRuleMatch: Sendable {
    public enum FactType: String, Sendable {
        case mcpServer = "mcp_server"
        case setting
        case `extension` = "extension"
        case authFile = "auth_file"
    }

    public let fact: FactType
    public let commandEquals: String?
    public let commandContains: String?
    public let nameContains: String?
    public let argsContain: String?
    public let app: String?
    public let key: String?
    public let valueEquals: String?
    public let extensionIdContains: String?

    public init(
        fact: FactType,
        commandEquals: String? = nil,
        commandContains: String? = nil,
        nameContains: String? = nil,
        argsContain: String? = nil,
        app: String? = nil,
        key: String? = nil,
        valueEquals: String? = nil,
        extensionIdContains: String? = nil
    ) {
        self.fact = fact
        self.commandEquals = commandEquals
        self.commandContains = commandContains
        self.nameContains = nameContains
        self.argsContain = argsContain
        self.app = app
        self.key = key
        self.valueEquals = valueEquals
        self.extensionIdContains = extensionIdContains
    }
}

public struct PackOverride: Sendable {
    public let id: String
    public let severity: Severity
}

public struct PackEscalation: Sendable {
    public let requires: [String]
    public let scope: String
    public let escalateTo: String
    public let reason: String
}

extension EscalationScope {
    init?(yamlValue: String) {
        switch yamlValue.lowercased() {
        case "per_server": self = .perServer
        case "global": self = .global
        default: return nil
        }
    }
}
