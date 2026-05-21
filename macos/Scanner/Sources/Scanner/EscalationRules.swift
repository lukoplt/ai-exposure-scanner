public enum EscalationRules {
    private static let fsShell   = "Broad filesystem access combined with shell execution creates a direct exfiltration path"
    private static let fsNet     = "Broad filesystem access combined with network capability enables silent data upload"
    private static let shellNet  = "Shell execution combined with network access enables remote code execution"
    private static let keyNet    = "Plaintext API key combined with network access enables immediate credential exfiltration"
    private static let globalNet = "Network access capability is exposed across three or more MCP servers — broad attack surface"

    public static let builtIn: [EscalationRule] = [
        // (MCP-001 OR MCP-003) + MCP-002 → Critical per-server
        EscalationRule(requiresRuleIds: ["AES-MCP-001", "AES-MCP-002"], scope: .perServer, escalateTo: .critical, reason: fsShell),
        EscalationRule(requiresRuleIds: ["AES-MCP-003", "AES-MCP-002"], scope: .perServer, escalateTo: .critical, reason: fsShell),
        // (MCP-001 OR MCP-003) + MCP-004 → Critical per-server
        EscalationRule(requiresRuleIds: ["AES-MCP-001", "AES-MCP-004"], scope: .perServer, escalateTo: .critical, reason: fsNet),
        EscalationRule(requiresRuleIds: ["AES-MCP-003", "AES-MCP-004"], scope: .perServer, escalateTo: .critical, reason: fsNet),
        // MCP-002 + MCP-004 → Critical per-server
        EscalationRule(requiresRuleIds: ["AES-MCP-002", "AES-MCP-004"], scope: .perServer, escalateTo: .critical, reason: shellNet),
        // (AUTH-001 OR AUTH-002) + MCP-004 → Critical per-server
        EscalationRule(requiresRuleIds: ["AES-AUTH-001", "AES-MCP-004"], scope: .perServer, escalateTo: .critical, reason: keyNet),
        EscalationRule(requiresRuleIds: ["AES-AUTH-002", "AES-MCP-004"], scope: .perServer, escalateTo: .critical, reason: keyNet),
        // MCP-004 on ≥ 3 distinct servers → Critical global
        EscalationRule(requiresRuleIds: ["AES-MCP-004"], scope: .global, escalateTo: .critical, reason: globalNet, minimumDistinctServers: 3),
    ]
}
