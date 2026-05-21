import Yams

public enum RulePackLoadResult: Sendable {
    case valid(RulePack)
    case invalid(String)
}

public enum RulePackLoader {
    public static func load(yaml: String) -> RulePackLoadResult {
        do {
            return .valid(try parse(yaml: yaml))
        } catch let e as ParseError {
            return .invalid(e.message)
        } catch {
            return .invalid("YAML parse error: \(error.localizedDescription)")
        }
    }

    // MARK: - Private

    private struct ParseError: Error {
        let message: String
    }

    private static func parse(yaml: String) throws -> RulePack {
        guard let parsed = try Yams.load(yaml: yaml) else {
            throw ParseError(message: "Empty YAML document")
        }
        guard let root = parsed as? [String: Any] else {
            throw ParseError(message: "Root must be a YAML mapping")
        }

        guard let version = root["version"] as? String, !version.isEmpty else {
            throw ParseError(message: "Missing required field: version")
        }
        guard let id = root["id"] as? String, !id.isEmpty else {
            throw ParseError(message: "Missing required field: id")
        }
        if id.uppercased().hasPrefix("AES-") {
            throw ParseError(message: "Rule pack id must not start with 'AES-' (reserved for built-in rules)")
        }
        guard let name = root["name"] as? String, !name.isEmpty else {
            throw ParseError(message: "Missing required field: name")
        }

        let rules: [PackRule] = try (root["rules"] as? [[String: Any]] ?? [])
            .enumerated().map { i, r in try parseRule(r, prefix: "rules[\(i)]") }

        let overrides: [PackOverride] = try (root["overrides"] as? [[String: Any]] ?? [])
            .enumerated().map { i, r in try parseOverride(r, prefix: "overrides[\(i)]") }

        let escalations: [PackEscalation] = try (root["escalations"] as? [[String: Any]] ?? [])
            .enumerated().map { i, r in try parseEscalation(r, prefix: "escalations[\(i)]") }

        return RulePack(version: version, id: id, name: name,
                        rules: rules, overrides: overrides, escalations: escalations)
    }

    private static func parseRule(_ raw: [String: Any], prefix: String) throws -> PackRule {
        guard let id = raw["id"] as? String, !id.isEmpty else {
            throw ParseError(message: "\(prefix): missing required field 'id'")
        }
        if id.uppercased().hasPrefix("AES-") {
            throw ParseError(message: "\(prefix): rule id '\(id)' must not start with 'AES-'")
        }
        guard let severityRaw = raw["severity"] as? String,
              let severity = Severity(rawValue: severityRaw) else {
            throw ParseError(message: "\(prefix): missing or invalid 'severity' (use: critical, high, medium, low)")
        }
        guard let matchRaw = raw["match"] as? [String: Any] else {
            throw ParseError(message: "\(prefix): missing required field 'match'")
        }
        return PackRule(
            id: id,
            severity: severity,
            title: raw["title"] as? String ?? "",
            explanation: raw["explanation"] as? String ?? "",
            recommendation: raw["recommendation"] as? String ?? "",
            match: try parseMatch(matchRaw, prefix: "\(prefix).match")
        )
    }

    private static func parseMatch(_ raw: [String: Any], prefix: String) throws -> PackRuleMatch {
        guard let factRaw = raw["fact"] as? String,
              let fact = PackRuleMatch.FactType(rawValue: factRaw) else {
            throw ParseError(message: "\(prefix): missing or invalid 'fact' (use: mcp_server, setting, extension, auth_file)")
        }
        return PackRuleMatch(
            fact: fact,
            commandEquals: raw["command_equals"] as? String,
            commandContains: raw["command_contains"] as? String,
            nameContains: raw["name_contains"] as? String,
            argsContain: raw["args_contain"] as? String,
            app: raw["app"] as? String,
            key: raw["key"] as? String,
            valueEquals: raw["value_equals"] as? String,
            extensionIdContains: raw["extension_id_contains"] as? String
        )
    }

    private static func parseOverride(_ raw: [String: Any], prefix: String) throws -> PackOverride {
        guard let id = raw["id"] as? String, !id.isEmpty else {
            throw ParseError(message: "\(prefix): missing required field 'id'")
        }
        guard let severityRaw = raw["severity"] as? String,
              let severity = Severity(rawValue: severityRaw) else {
            throw ParseError(message: "\(prefix): missing or invalid 'severity'")
        }
        return PackOverride(id: id, severity: severity)
    }

    private static func parseEscalation(_ raw: [String: Any], prefix: String) throws -> PackEscalation {
        guard let requires = raw["requires"] as? [String], !requires.isEmpty else {
            throw ParseError(message: "\(prefix): missing or empty 'requires' (list of rule IDs)")
        }
        guard let scope = raw["scope"] as? String, EscalationScope(yamlValue: scope) != nil else {
            throw ParseError(message: "\(prefix): missing or invalid 'scope' (use: per_server, global)")
        }
        guard let escalateTo = raw["escalate_to"] as? String, Severity(rawValue: escalateTo) != nil else {
            throw ParseError(message: "\(prefix): missing or invalid 'escalate_to' (use: critical, high, medium, low)")
        }
        return PackEscalation(
            requires: requires,
            scope: scope,
            escalateTo: escalateTo,
            reason: raw["reason"] as? String ?? ""
        )
    }
}
