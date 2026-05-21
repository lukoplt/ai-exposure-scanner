import Foundation
import Scanner

try runFixtureCorpus()
try runEscalationEvaluatorTests()
try runRuleUnitTests()
try runDetectorIntegrationTests()
try runReportBuilderTests()
try runRulePackLoaderTests()
print("ScannerFixtureTests passed")

private func runFixtureCorpus() throws {
    let fixtureRoot = repoRoot().appendingPathComponent("fixtures")
    let inputFiles = try FileManager.default
        .subpathsOfDirectory(atPath: fixtureRoot.path)
        .filter { $0.hasSuffix("/input.json") }
        .sorted()

    try expect(!inputFiles.isEmpty, "No fixture input files found")

    for relativeInput in inputFiles {
        let inputURL = fixtureRoot.appendingPathComponent(relativeInput)
        let expectedURL = inputURL
            .deletingLastPathComponent()
            .appendingPathComponent("expected.json")
        let appId = inputURL
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .lastPathComponent

        let facts = try FixtureFactBuilder.facts(from: inputURL, appId: appId)
        let builtInFindings = RuleEvaluator().evaluate(facts)

        // Parse optional rulePacks from input.json
        let inputData = try Data(contentsOf: inputURL)
        let inputObj = try JSONSerialization.jsonObject(with: inputData)
        var packsForFixture: [RulePack] = []
        if let root = inputObj as? [String: Any],
           let yamlStrings = root["rulePacks"] as? [String] {
            packsForFixture = yamlStrings.compactMap { yaml in
                if case .valid(let pack) = RulePackLoader.load(yaml: yaml) { return pack }
                return nil
            }
        }

        let allFindings = RulePackEvaluator().evaluate(
            facts: facts, packs: packsForFixture, existingFindings: builtInFindings)
        let escalationRules = EscalationRules.builtIn + packsForFixture.flatMap(\.escalationRules)
        let escalated = EscalationEvaluator().evaluate(allFindings, rules: escalationRules)

        let actual = escalated.map(ComparableFinding.init)
        let expected = try ExpectedReport.load(from: expectedURL)
            .findings
            .map(ComparableFinding.init)

        try expect(actual == expected, "Fixture mismatch: \(relativeInput)\nactual: \(actual)\nexpected: \(expected)")
    }
}

private func runEscalationEvaluatorTests() throws {
    let mcp003Finding = Finding(
        ruleId: "AES-MCP-003", severity: .high, app: "claude-desktop", serverName: "fs",
        title: "t", explanation: "e", recommendation: "r"
    )
    let mcp004Finding = Finding(
        ruleId: "AES-MCP-004", severity: .high, app: "claude-desktop", serverName: "fs",
        title: "t", explanation: "e", recommendation: "r"
    )
    let perServerRule = EscalationRule(
        requiresRuleIds: ["AES-MCP-003", "AES-MCP-004"],
        scope: .perServer,
        escalateTo: .critical,
        reason: "test reason"
    )

    let escalated = EscalationEvaluator().evaluate([mcp003Finding, mcp004Finding], rules: [perServerRule])
    try expect(
        escalated.allSatisfy { $0.severity == .critical && $0.escalationReason == "test reason" },
        "Per-server escalation should upgrade both findings to critical with reason"
    )

    let notEscalated = EscalationEvaluator().evaluate([mcp003Finding], rules: [perServerRule])
    try expect(
        notEscalated.allSatisfy { $0.escalationReason == nil },
        "Should not escalate when not all required rule IDs are present"
    )

    // Different servers — same ruleIds but on different servers, per-server should NOT fire
    let serverAFinding = Finding(
        ruleId: "AES-MCP-003", severity: .high, app: "claude-desktop", serverName: "server-a",
        title: "t", explanation: "e", recommendation: "r"
    )
    let serverBFinding = Finding(
        ruleId: "AES-MCP-004", severity: .high, app: "claude-desktop", serverName: "server-b",
        title: "t", explanation: "e", recommendation: "r"
    )
    let splitServers = EscalationEvaluator().evaluate([serverAFinding, serverBFinding], rules: [perServerRule])
    try expect(
        splitServers.allSatisfy { $0.escalationReason == nil },
        "Per-server escalation should not fire when required rules are on different servers"
    )

    // Global scope: 3 distinct servers
    let globalRule = EscalationRule(
        requiresRuleIds: ["AES-MCP-004"],
        scope: .global,
        escalateTo: .critical,
        reason: "global test",
        minimumDistinctServers: 3
    )
    let findings3Servers = (1...3).map { i in
        Finding(
            ruleId: "AES-MCP-004", severity: .high, app: "claude-desktop", serverName: "server-\(i)",
            title: "t", explanation: "e", recommendation: "r"
        )
    }
    let escalatedGlobal = EscalationEvaluator().evaluate(findings3Servers, rules: [globalRule])
    try expect(
        escalatedGlobal.allSatisfy { $0.severity == .critical },
        "Global escalation should fire when 3+ distinct servers match"
    )

    let findings2Servers = Array(findings3Servers.dropLast())
    let notEscalatedGlobal = EscalationEvaluator().evaluate(findings2Servers, rules: [globalRule])
    try expect(
        notEscalatedGlobal.allSatisfy { $0.escalationReason == nil },
        "Global escalation should not fire with fewer than minimumDistinctServers"
    )

    // Already at target severity — no change
    let alreadyCritical = Finding(
        ruleId: "AES-MCP-002", severity: .critical, app: "claude-desktop", serverName: "fs",
        title: "t", explanation: "e", recommendation: "r"
    )
    let noChangeResult = EscalationEvaluator().evaluate([alreadyCritical, mcp004Finding], rules: [perServerRule])
    try expect(
        noChangeResult.first { $0.ruleId == "AES-MCP-002" }?.escalationReason == nil,
        "Should not add escalationReason to finding already at target severity"
    )
}

private func runRuleUnitTests() throws {
    try expect(
        RuleEvaluator(rules: [AESMCP003()])
            .evaluate(
                ScanFacts(
                    mcpServers: [
                        McpServerFact(
                            appId: "claude-desktop",
                            name: "documents",
                            command: "npx",
                            args: ["server", "/Users/testuser/Documents"]
                        )
                    ]
                )
            )
            .map(\.ruleId) == ["AES-MCP-003"],
        "AES-MCP-003 should detect broad filesystem paths"
    )

    try expect(
        RuleEvaluator(rules: [AESAUTH002()])
            .evaluate(
                ScanFacts(
                    mcpServers: [
                        McpServerFact(
                            appId: "claude-desktop",
                            name: "arg-secret",
                            command: "npx",
                            args: ["sk-proj-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwx"]
                        )
                    ]
                )
            )
            .map(\.ruleId) == ["AES-AUTH-002"],
        "AES-AUTH-002 should detect API keys in args"
    )

    try expect(
        RuleEvaluator(rules: [AESMCP005()])
            .evaluate(
                ScanFacts(
                    mcpServers: [
                        McpServerFact(appId: "cursor", name: "browser", command: "npx", args: ["@playwright/mcp"])
                    ]
                )
            )
            .map(\.ruleId) == ["AES-MCP-005"],
        "AES-MCP-005 should detect browser automation servers"
    )

    let duplicateFindings = RuleEvaluator(rules: [AESCFG002()])
        .evaluate(
            ScanFacts(
                mcpServers: [
                    McpServerFact(appId: "claude-desktop", name: "filesystem", command: "npx"),
                    McpServerFact(appId: "cursor", name: "filesystem", command: "npx")
                ]
            )
        )

    try expect(
        duplicateFindings.map(\.app) == ["claude-desktop", "cursor"] &&
            duplicateFindings.map(\.ruleId) == ["AES-CFG-002", "AES-CFG-002"],
        "AES-CFG-002 should report each client with a duplicate server"
    )

    try expect(
        RuleEvaluator(rules: [AESCFG003()])
            .evaluate(
                ScanFacts(
                    mcpServers: [
                        McpServerFact(appId: "windsurf", name: "files", command: "npx")
                    ]
                )
            )
            .map(\.ruleId) == ["AES-CFG-003"],
        "AES-CFG-003 should detect missing server descriptions"
    )

    try expect(
        RuleEvaluator(rules: [AESCFG004()])
            .evaluate(
                ScanFacts(
                    configFiles: [
                        ConfigFileFact(appId: "cursor", path: "~/.cursor/mcp.json", worldReadable: true)
                    ]
                )
            )
            .map(\.ruleId) == ["AES-CFG-004"],
        "AES-CFG-004 should detect world-readable config files"
    )

    try expect(
        RuleEvaluator(rules: [AESMCP006()])
            .evaluate(
                ScanFacts(
                    mcpServers: [
                        McpServerFact(appId: "cursor", name: "old-server", command: "npx", configPath: "~/.cursor/mcp.json")
                    ],
                    appInstallations: [
                        AppInstallationFact(appId: "cursor", installed: false)
                    ]
                )
            )
            .map(\.ruleId) == ["AES-MCP-006"],
        "AES-MCP-006 should detect MCP config left behind by an uninstalled app"
    )
}

private func runDetectorIntegrationTests() throws {
    let home = "/Users/testuser"
    let fs = InMemoryFilesystem(
        homeDirectory: home,
        files: [
            "\(home)/Library/Application Support/Claude/claude_desktop_config.json": InMemoryFile(
                text: """
                {
                  "mcpServers": {
                    "filesystem": {
                      "command": "npx",
                      "args": ["-y", "@modelcontextprotocol/server-filesystem@1.2.3", "/Users/testuser"],
                      "description": "Home filesystem access"
                    }
                  }
                }
                """
            ),
            "\(home)/Library/Application Support/Cursor/User/settings.json": InMemoryFile(
                text: #"{"cursor.privacyMode": false}"#
            ),
            "\(home)/.codex/config.toml": InMemoryFile(
                text: """
                [mcp_servers.fetch]
                command = "npx"
                args = ["-y", "@modelcontextprotocol/server-fetch"]
                description = "Fetch remote URLs"
                """
            ),
            "\(home)/.codex/auth.json": InMemoryFile(text: "{}"),
            "\(home)/.gemini/settings.json": InMemoryFile(
                text: #"{"apiKey":"AIzaSyFAKEFAKEFAKEFAKEFAKEFAKEFAKEFAKEF","mcpServers":{}}"#
            )
        ],
        directories: [
            "/Applications/Claude.app",
            "/Applications/Cursor.app",
            "/Applications/Visual Studio Code.app",
            "\(home)/.vscode/extensions/saoudrizwan.claude-dev-2.1.0"
        ]
    )

    let result = try ScanOrchestrator().scan(fs: fs)
    let summary = Set(result.findings.map { "\($0.ruleId)|\($0.app)|\($0.serverName ?? "nil")|\($0.extensionId ?? "nil")" })

    let expected: Set<String> = [
        "AES-MCP-001|claude-desktop|filesystem|nil",
        "AES-CFG-001|cursor|nil|nil",
        "AES-MCP-004|codex-cli|fetch|nil",
        "AES-MCP-007|codex-cli|fetch|nil",
        "AES-AUTH-003|codex-cli|nil|nil",
        "AES-AUTH-001|gemini-cli|nil|nil",
        "AES-EXT-001|vscode|nil|saoudrizwan.claude-dev"
    ]

    try expect(expected.isSubset(of: summary), "Detector integration missing findings: \(expected.subtracting(summary))")
}

private func runReportBuilderTests() throws {
    let secret = "sk-proj-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwx"
    let facts = ScanFacts(
        mcpServers: [
            McpServerFact(
                appId: "claude-desktop",
                name: "arg-secret",
                command: "node",
                args: [secret],
                description: "Synthetic secret argument"
            )
        ]
    )
    let result = ScanResult(facts: facts, findings: RuleEvaluator().evaluate(facts))
    let summary = ReportSummary(scanResult: result)

    try expect(summary.status == .critical, "Report summary should use worst finding status")
    try expect(summary.toolsFound == 1, "Report summary should count apps with facts")
    try expect(summary.mcpServersFound == 1, "Report summary should count MCP servers")
    try expect(summary.critical == 1, "Report summary should count critical findings")

    let builder = ReportBuilder()
    let markdown = builder.markdown(scanResult: result, scannedAt: Date(timeIntervalSince1970: 0))
    let html = builder.html(scanResult: result, scannedAt: Date(timeIntervalSince1970: 0))

    try expect(markdown.contains("# AI Exposure Scanner Report"), "Markdown report should include title")
    try expect(markdown.contains("AES-AUTH-002"), "Markdown report should include rule ID")
    try expect(markdown.contains("************"), "Markdown report should include masked secret preview")
    try expect(!markdown.contains(secret), "Markdown report must not include full secret")
    try expect(html.contains("<!doctype html>"), "HTML report should include document wrapper")
    try expect(html.contains("AES-AUTH-002"), "HTML report should include rule ID")
    try expect(html.contains("************"), "HTML report should include masked secret preview")
    try expect(!html.contains(secret), "HTML report must not include full secret")

    let json = builder.json(scanResult: result, scannedAt: Date(timeIntervalSince1970: 0))
    let jsonData = json.data(using: .utf8)!
    let decoded = try JSONSerialization.jsonObject(with: jsonData) as! [String: Any]
    try expect(decoded["schemaVersion"] as? String == "1.0.0", "JSON report should include schemaVersion")
    try expect(decoded["platform"] as? String == "macos", "JSON report should include platform")
    let findings = decoded["findings"] as! [[String: Any]]
    try expect(findings.contains { $0["ruleId"] as? String == "AES-AUTH-002" }, "JSON report should include finding ruleId")
    try expect(!json.contains(secret), "JSON report must not include full secret")
}

private func runRulePackLoaderTests() throws {
    // Valid minimal pack
    let validYaml = """
        version: "1.0"
        id: test-pack
        name: Test Pack
        """
    if case .invalid(let msg) = RulePackLoader.load(yaml: validYaml) {
        throw TestFailure("Valid minimal pack rejected: \(msg)")
    }

    // Reserved AES- id
    let reservedYaml = """
        version: "1.0"
        id: AES-CUSTOM
        name: Bad Pack
        """
    guard case .invalid = RulePackLoader.load(yaml: reservedYaml) else {
        throw TestFailure("Pack with AES- id should be rejected")
    }

    // Missing version
    guard case .invalid = RulePackLoader.load(yaml: "id: test-pack\nname: X") else {
        throw TestFailure("Pack missing version should be rejected")
    }

    // Custom rule with valid match
    let packWithRule = """
        version: "1.0"
        id: my-org-rules
        name: My Org Rules
        rules:
          - id: MY-001
            severity: high
            title: Custom Rule
            explanation: Test
            recommendation: Fix it
            match:
              fact: mcp_server
              name_contains: suspicious
        """
    switch RulePackLoader.load(yaml: packWithRule) {
    case .valid(let pack):
        try expect(pack.rules.count == 1, "Pack should have 1 rule")
        try expect(pack.rules[0].id == "MY-001", "Rule id should be MY-001")
        try expect(pack.rules[0].severity == .high, "Rule severity should be high")
    case .invalid(let msg):
        throw TestFailure("Valid pack with rule rejected: \(msg)")
    }

    // Rule with AES- id rejected
    let badRuleYaml = """
        version: "1.0"
        id: org-rules
        name: Org
        rules:
          - id: AES-MCP-001
            severity: low
            title: Bad
            explanation: X
            recommendation: X
            match:
              fact: mcp_server
        """
    guard case .invalid = RulePackLoader.load(yaml: badRuleYaml) else {
        throw TestFailure("Rule with AES- id should be rejected")
    }

    // Override + escalation parsed correctly
    let fullPack = """
        version: "1.0"
        id: full-pack
        name: Full Pack
        overrides:
          - id: AES-MCP-007
            severity: low
        escalations:
          - requires:
              - AES-MCP-003
              - AES-MCP-004
            scope: per_server
            escalate_to: critical
            reason: Test escalation
        """
    switch RulePackLoader.load(yaml: fullPack) {
    case .valid(let pack):
        try expect(pack.overrides.count == 1, "Pack should have 1 override")
        try expect(pack.escalations.count == 1, "Pack should have 1 escalation")
        try expect(pack.escalationRules.count == 1, "Pack escalationRules should have 1 entry")
        try expect(pack.escalationRules[0].escalateTo == .critical, "Escalation target should be critical")
    case .invalid(let msg):
        throw TestFailure("Full pack rejected: \(msg)")
    }
}

private func repoRoot() -> URL {
    var url = URL(fileURLWithPath: #filePath)
    for _ in 0..<5 {
        url.deleteLastPathComponent()
    }
    return url
}

private func expect(_ condition: Bool, _ message: String) throws {
    if !condition {
        throw TestFailure(message)
    }
}

private struct TestFailure: Error, CustomStringConvertible {
    let message: String

    init(_ message: String) {
        self.message = message
    }

    var description: String {
        message
    }
}

private struct InMemoryFile {
    let text: String
    let worldReadable: Bool

    init(text: String, worldReadable: Bool = false) {
        self.text = text
        self.worldReadable = worldReadable
    }
}

private struct InMemoryFilesystem: FilesystemFacade {
    let homeDirectory: String
    let files: [String: InMemoryFile]
    let directories: Set<String>

    func fileExists(_ path: String) -> Bool {
        files[path] != nil
    }

    func directoryExists(_ path: String) -> Bool {
        allDirectories.contains(normalizeDirectory(path))
    }

    func readTextFile(_ path: String, maxBytes: Int) throws -> String {
        guard let file = files[path] else {
            throw TestFailure("Missing in-memory file: \(path)")
        }
        return file.text
    }

    func listDirectoryNames(_ path: String) throws -> [String] {
        let directory = normalizeDirectory(path)
        var names = Set<String>()
        for candidate in allDirectories where candidate.hasPrefix(directory + "/") {
            let remainder = String(candidate.dropFirst(directory.count + 1))
            if let first = remainder.split(separator: "/", maxSplits: 1).first {
                names.insert(String(first))
            }
        }
        for filePath in files.keys where filePath.hasPrefix(directory + "/") {
            let remainder = String(filePath.dropFirst(directory.count + 1))
            if let first = remainder.split(separator: "/", maxSplits: 1).first {
                names.insert(String(first))
            }
        }
        return names.sorted()
    }

    func listFilesRecursively(_ path: String, maxDepth: Int) throws -> [String] {
        let root = normalizeDirectory(path)
        return files.keys.filter { filePath in
            guard filePath.hasPrefix(root + "/") else {
                return false
            }
            let remainder = String(filePath.dropFirst(root.count + 1))
            return remainder.split(separator: "/").count <= maxDepth + 1
        }.sorted()
    }

    func isWorldReadableFile(_ path: String) -> Bool {
        files[path]?.worldReadable ?? false
    }

    private var allDirectories: Set<String> {
        var result = Set(directories.map { normalizeDirectory($0) })
        result.insert(homeDirectory)
        for directory in directories {
            insertParentDirectories(of: directory, into: &result)
        }
        for path in files.keys {
            insertParentDirectories(of: path, into: &result)
        }
        return Set(result)
    }

    private func normalizeDirectory(_ path: String) -> String {
        path.hasSuffix("/") ? String(path.dropLast()) : path
    }

    private func insertParentDirectories(of path: String, into result: inout Set<String>) {
        var components = normalizeDirectory(path).split(separator: "/").map(String.init)
        guard !components.isEmpty else {
            return
        }
        if files[path] != nil {
            components.removeLast()
        }
        var current = ""
        for component in components {
            current += "/" + component
            result.insert(current)
        }
    }
}

private enum FixtureFactBuilder {
    static func facts(from url: URL, appId: String) throws -> ScanFacts {
        let data = try Data(contentsOf: url)
        let object = try JSONSerialization.jsonObject(with: data)
        guard let root = object as? [String: Any] else {
            return ScanFacts()
        }

        var facts = ScanFacts()

        if let servers = root["mcpServers"] as? [String: Any] {
            facts.mcpServers.append(contentsOf: parseMcpServers(servers, appId: appId, configPath: url.path))
        }

        if let type = root["_type"] as? String {
            switch type {
            case "cursor-settings":
                facts.settings.append(
                    SettingFact(
                        appId: appId,
                        key: "cursor.privacyMode",
                        boolValue: root["cursor.privacyMode"] as? Bool,
                        configPath: url.path
                    )
                )
            case "cursor-mcp":
                if let servers = root["mcpServers"] as? [String: Any] {
                    facts.mcpServers.append(contentsOf: parseMcpServers(servers, appId: appId, configPath: url.path))
                }
            case "codex-cli":
                if root["hasAuthFile"] as? Bool == true {
                    facts.authFiles.append(AuthFileFact(appId: appId, filePath: "~/.codex/auth.json"))
                }
                if let servers = root["mcpServers"] as? [String: Any] {
                    facts.mcpServers.append(contentsOf: parseMcpServers(servers, appId: appId, configPath: url.path))
                }
            case "gemini-cli-settings":
                if let apiKey = root["apiKey"] as? String {
                    facts.settings.append(
                        SettingFact(appId: appId, key: "apiKey", stringValue: apiKey, configPath: url.path)
                    )
                }
                if let hasCredentials = root["hasCredentialsFile"] as? Bool, hasCredentials {
                    facts.authFiles.append(AuthFileFact(appId: appId, filePath: "~/.gemini/credentials"))
                }
                if let servers = root["mcpServers"] as? [String: Any] {
                    facts.mcpServers.append(contentsOf: parseMcpServers(servers, appId: appId, configPath: url.path))
                }
            case "vscode-extensions":
                facts.extensions.append(contentsOf: parseExtensions(root["extensions"] as? [String] ?? [], appId: appId))
            case "cross-client":
                if let clients = root["clients"] as? [String: Any] {
                    for (clientId, value) in clients {
                        guard
                            let client = value as? [String: Any],
                            let servers = client["mcpServers"] as? [String: Any]
                        else {
                            continue
                        }
                        facts.mcpServers.append(contentsOf: parseMcpServers(servers, appId: clientId, configPath: url.path))
                    }
                }
            default:
                break
            }
        }

        if root["configFileWorldReadable"] as? Bool == true {
            facts.configFiles.append(ConfigFileFact(appId: appId, path: url.path, worldReadable: true))
        }

        return facts
    }

    private static func parseMcpServers(
        _ servers: [String: Any],
        appId: String,
        configPath: String
    ) -> [McpServerFact] {
        servers.compactMap { name, value in
            guard let config = value as? [String: Any] else {
                return nil
            }
            return McpServerFact(
                appId: appId,
                name: name,
                command: config["command"] as? String ?? "",
                args: config["args"] as? [String] ?? [],
                env: config["env"] as? [String: String] ?? [:],
                disabled: config["disabled"] as? Bool ?? false,
                description: config["description"] as? String,
                configPath: configPath
            )
        }
    }

    private static func parseExtensions(_ extensions: [String], appId: String) -> [ExtensionFact] {
        extensions.compactMap { folder in
            if folder.hasPrefix("saoudrizwan.claude-dev-") {
                return ExtensionFact(
                    appId: appId,
                    extensionId: "saoudrizwan.claude-dev",
                    extensionName: "Cline",
                    hasTerminalAccess: true
                )
            }
            if folder.hasPrefix("rooveterinaryinc.roo-cline-") {
                return ExtensionFact(
                    appId: appId,
                    extensionId: "rooveterinaryinc.roo-cline",
                    extensionName: "Roo",
                    hasTerminalAccess: true
                )
            }
            if folder.hasPrefix("continue.continue-") {
                return ExtensionFact(
                    appId: appId,
                    extensionId: "continue.continue",
                    extensionName: "Continue",
                    hasTerminalAccess: true
                )
            }
            if folder.hasPrefix("github.copilot-") {
                return ExtensionFact(
                    appId: appId,
                    extensionId: "github.copilot",
                    extensionName: "GitHub Copilot",
                    hasTerminalAccess: false
                )
            }
            return nil
        }
    }
}

private struct ExpectedReport: Decodable {
    let findings: [ExpectedFinding]

    static func load(from url: URL) throws -> ExpectedReport {
        let data = try Data(contentsOf: url)
        return try JSONDecoder().decode(ExpectedReport.self, from: data)
    }
}

private struct ExpectedFinding: Decodable {
    let ruleId: String
    let severity: String
    let app: String
    let serverName: String?
    let extensionId: String?
    let escalationReason: String?
}

private struct ComparableFinding: Equatable, CustomStringConvertible {
    let ruleId: String
    let severity: String
    let app: String
    let serverName: String?
    let extensionId: String?
    let escalationReason: String?

    init(_ finding: Finding) {
        ruleId = finding.ruleId
        severity = finding.severity.rawValue
        app = finding.app
        serverName = finding.serverName
        extensionId = finding.extensionId
        escalationReason = finding.escalationReason
    }

    init(_ finding: ExpectedFinding) {
        ruleId = finding.ruleId
        severity = finding.severity
        app = finding.app
        serverName = finding.serverName
        extensionId = finding.extensionId
        escalationReason = finding.escalationReason
    }

    var description: String {
        "\(severity) \(ruleId) \(app) \(serverName ?? "nil") \(extensionId ?? "nil") esc=\(escalationReason ?? "nil")"
    }
}
