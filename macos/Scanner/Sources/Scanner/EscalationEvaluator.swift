public struct EscalationEvaluator: Sendable {
    public init() {}

    public func evaluate(_ findings: [Finding], rules: [EscalationRule]) -> [Finding] {
        var result = findings
        for rule in rules {
            result = apply(rule, to: result)
        }
        return result
    }

    private func apply(_ rule: EscalationRule, to findings: [Finding]) -> [Finding] {
        switch rule.scope {
        case .perServer:
            return applyPerServer(rule, to: findings)
        case .global:
            return applyGlobal(rule, to: findings)
        }
    }

    private func applyPerServer(_ rule: EscalationRule, to findings: [Finding]) -> [Finding] {
        let groups = Dictionary(grouping: findings) { "\($0.app)||\($0.serverName ?? "")" }
        var result = findings
        for (_, group) in groups {
            let ruleIds = Set(group.map(\.ruleId))
            guard rule.requiresRuleIds.isSubset(of: ruleIds) else { continue }
            result = result.map { finding in
                guard
                    group.contains(finding),
                    rule.requiresRuleIds.contains(finding.ruleId),
                    finding.severity.sortRank > rule.escalateTo.sortRank
                else { return finding }
                return escalated(finding, to: rule)
            }
        }
        return result
    }

    private func applyGlobal(_ rule: EscalationRule, to findings: [Finding]) -> [Finding] {
        let allRuleIds = Set(findings.map(\.ruleId))
        guard rule.requiresRuleIds.isSubset(of: allRuleIds) else { return findings }

        if rule.minimumDistinctServers > 1 {
            for reqId in rule.requiresRuleIds {
                let distinctServers = Set(findings.filter { $0.ruleId == reqId }.compactMap(\.serverName))
                if distinctServers.count < rule.minimumDistinctServers { return findings }
            }
        }

        return findings.map { finding in
            guard
                rule.requiresRuleIds.contains(finding.ruleId),
                finding.severity.sortRank > rule.escalateTo.sortRank
            else { return finding }
            return escalated(finding, to: rule)
        }
    }

    private func escalated(_ finding: Finding, to rule: EscalationRule) -> Finding {
        Finding(
            ruleId: finding.ruleId,
            severity: rule.escalateTo,
            app: finding.app,
            serverName: finding.serverName,
            extensionId: finding.extensionId,
            affectedPath: finding.affectedPath,
            maskedValue: finding.maskedValue,
            title: finding.title,
            explanation: finding.explanation,
            recommendation: finding.recommendation,
            escalationReason: rule.reason
        )
    }
}
