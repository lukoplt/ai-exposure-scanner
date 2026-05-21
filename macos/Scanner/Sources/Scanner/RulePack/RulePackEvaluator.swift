public struct RulePackEvaluator: Sendable {
    public init() {}

    public func evaluate(
        facts: ScanFacts,
        packs: [RulePack],
        existingFindings: [Finding]
    ) -> [Finding] {
        var findings = existingFindings

        for pack in packs {
            // 1. Custom rules → new findings
            for rule in pack.rules {
                findings.append(contentsOf: matchRule(rule, against: facts))
            }

            // 2. Overrides — replace severity on matched rule IDs
            let overrideMap = Dictionary(
                uniqueKeysWithValues: pack.overrides.map { ($0.id, $0.severity) }
            )
            if !overrideMap.isEmpty {
                findings = findings.map { finding in
                    guard let newSeverity = overrideMap[finding.ruleId] else {
                        return finding
                    }
                    return Finding(
                        ruleId: finding.ruleId,
                        severity: newSeverity,
                        app: finding.app,
                        serverName: finding.serverName,
                        extensionId: finding.extensionId,
                        affectedPath: finding.affectedPath,
                        maskedValue: finding.maskedValue,
                        title: finding.title,
                        explanation: finding.explanation,
                        recommendation: finding.recommendation,
                        escalationReason: finding.escalationReason
                    )
                }
            }
        }

        return findings
    }

    // MARK: - Match

    private func matchRule(_ rule: PackRule, against facts: ScanFacts) -> [Finding] {
        switch rule.match.fact {
        case .mcpServer:
            return facts.mcpServers
                .filter { matches(server: $0, match: rule.match) }
                .map { server in
                    Finding(
                        ruleId: rule.id,
                        severity: rule.severity,
                        app: server.appId,
                        serverName: server.name,
                        affectedPath: server.configPath,
                        title: rule.title,
                        explanation: rule.explanation,
                        recommendation: rule.recommendation
                    )
                }

        case .setting:
            return facts.settings
                .filter { matches(setting: $0, match: rule.match) }
                .map { setting in
                    Finding(
                        ruleId: rule.id,
                        severity: rule.severity,
                        app: setting.appId,
                        affectedPath: setting.configPath,
                        title: rule.title,
                        explanation: rule.explanation,
                        recommendation: rule.recommendation
                    )
                }

        case .extension:
            return facts.extensions
                .filter { matches(ext: $0, match: rule.match) }
                .map { ext in
                    Finding(
                        ruleId: rule.id,
                        severity: rule.severity,
                        app: ext.appId,
                        extensionId: ext.extensionId,
                        title: rule.title,
                        explanation: rule.explanation,
                        recommendation: rule.recommendation
                    )
                }

        case .authFile:
            return facts.authFiles
                .filter { matches(auth: $0, match: rule.match) }
                .map { auth in
                    Finding(
                        ruleId: rule.id,
                        severity: rule.severity,
                        app: auth.appId,
                        affectedPath: auth.filePath,
                        title: rule.title,
                        explanation: rule.explanation,
                        recommendation: rule.recommendation
                    )
                }
        }
    }

    // MARK: - Predicate helpers

    private func matches(server: McpServerFact, match: PackRuleMatch) -> Bool {
        if let v = match.commandEquals, server.commandName != v { return false }
        if let v = match.commandContains, !server.commandName.contains(v) { return false }
        if let v = match.nameContains, !server.name.contains(v) { return false }
        if let v = match.argsContain, !server.args.contains(where: { $0.contains(v) }) { return false }
        if let v = match.app, server.appId != v { return false }
        return true
    }

    private func matches(setting: SettingFact, match: PackRuleMatch) -> Bool {
        if let v = match.key, setting.key != v { return false }
        if let v = match.valueEquals, setting.stringValue != v { return false }
        if let v = match.app, setting.appId != v { return false }
        return true
    }

    private func matches(ext: ExtensionFact, match: PackRuleMatch) -> Bool {
        if let v = match.extensionIdContains, !ext.extensionId.contains(v) { return false }
        if let v = match.app, ext.appId != v { return false }
        return true
    }

    private func matches(auth: AuthFileFact, match: PackRuleMatch) -> Bool {
        if let v = match.app, auth.appId != v { return false }
        return true
    }
}
