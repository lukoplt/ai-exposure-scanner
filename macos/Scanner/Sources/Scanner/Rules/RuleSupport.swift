import Foundation

struct RuleText: Sendable {
    let title: String
    let explanation: String
    let recommendation: String
}

enum RuleTexts {
    static let byId: [String: RuleText] = [
        "AES-MCP-001": RuleText(
            title: "MCP server has broad home directory access",
            explanation: "The MCP server can read from a broad home directory scope.",
            recommendation: "Restrict the server to a specific project folder."
        ),
        "AES-MCP-002": RuleText(
            title: "MCP server allows shell command execution",
            explanation: "The MCP server can run shell commands on this machine.",
            recommendation: "Remove this server or isolate it in a sandboxed environment."
        ),
        "AES-AUTH-001": RuleText(
            title: "API key in plain text in MCP server env",
            explanation: "A plain text API key is stored in configuration.",
            recommendation: "Move the key to the OS keychain or a supported secrets manager."
        ),
        "AES-AUTH-002": RuleText(
            title: "API key in plain text in MCP server args",
            explanation: "A plain text API key is passed on the command line.",
            recommendation: "Never pass API keys as command-line arguments."
        ),
        "AES-MCP-003": RuleText(
            title: "MCP server has filesystem access to a broad path",
            explanation: "The MCP server can access a broad filesystem location.",
            recommendation: "Restrict access to the narrow project folder required for the workflow."
        ),
        "AES-MCP-004": RuleText(
            title: "MCP server has network access",
            explanation: "The MCP server can fetch remote content or send data over the network.",
            recommendation: "Disable network-capable MCP servers unless they are required."
        ),
        "AES-MCP-005": RuleText(
            title: "MCP server reads browser data",
            explanation: "The MCP server can automate or inspect browser sessions.",
            recommendation: "Disable browser automation servers when they are not actively needed."
        ),
        "AES-EXT-001": RuleText(
            title: "AI coding extension can access terminal",
            explanation: "The extension can execute commands through the VS Code terminal.",
            recommendation: "Disable the extension in sensitive workspaces when not in use."
        ),
        "AES-CFG-001": RuleText(
            title: "Cursor Privacy Mode disabled",
            explanation: "Cursor may send workspace code for AI processing.",
            recommendation: "Enable Cursor Privacy Mode."
        ),
        "AES-MCP-006": RuleText(
            title: "Unused MCP server still configured",
            explanation: "A disabled or orphaned MCP server remains in configuration.",
            recommendation: "Remove stale MCP server entries."
        ),
        "AES-MCP-007": RuleText(
            title: "MCP server runs without pinned version",
            explanation: "The MCP server package can change between runs.",
            recommendation: "Pin the MCP server package to an exact version."
        ),
        "AES-AUTH-003": RuleText(
            title: "Auth token file present in plain text",
            explanation: "A local auth token file exists in a known AI tool location.",
            recommendation: "Review whether the token is still needed and keep it outside broad MCP scopes."
        ),
        "AES-CFG-002": RuleText(
            title: "Overlapping MCP servers across clients",
            explanation: "The same MCP server name is configured in multiple AI clients.",
            recommendation: "Consolidate duplicate MCP server registrations."
        ),
        "AES-CFG-003": RuleText(
            title: "MCP server has no description",
            explanation: "The MCP server has no documented purpose or expected access scope.",
            recommendation: "Add a description explaining why this server exists."
        ),
        "AES-CFG-004": RuleText(
            title: "AI tool config file is world-readable",
            explanation: "A configuration file can be read by other users or broad principals.",
            recommendation: "Restrict the config file permissions."
        )
    ]
}

extension Rule {
    func finding(
        app: String,
        serverName: String? = nil,
        extensionId: String? = nil,
        affectedPath: String? = nil,
        maskedValue: String? = nil
    ) -> Finding {
        guard let text = RuleTexts.byId[id] else {
            fatalError("Missing RuleText for rule ID '\(id)' — add an entry to RuleTexts.byId")
        }
        return Finding(
            ruleId: id,
            severity: severity,
            app: app,
            serverName: serverName,
            extensionId: extensionId,
            affectedPath: affectedPath,
            maskedValue: maskedValue,
            title: text.title,
            explanation: text.explanation,
            recommendation: text.recommendation
        )
    }
}

extension McpServerFact {
    var commandName: String {
        command
            .split(whereSeparator: { $0 == "/" || $0 == "\\" })
            .last
            .map(String.init) ?? command
    }

    var searchableValues: [String] {
        [command] + args + Array(env.values)
    }
}

enum SecretPatterns {
    private static let expressions: [NSRegularExpression] = [
        try! NSRegularExpression(pattern: #"sk-ant-[a-zA-Z0-9\-_]{80,}"#),
        try! NSRegularExpression(pattern: #"sk-[a-zA-Z0-9]{48}"#),
        try! NSRegularExpression(pattern: #"sk-proj-[a-zA-Z0-9\-_]{48,}"#),
        try! NSRegularExpression(pattern: #"AIza[a-zA-Z0-9\-_]{35,}"#),
        try! NSRegularExpression(pattern: #"ghp_[a-zA-Z0-9]{36}"#),
        try! NSRegularExpression(pattern: #"github_pat_[a-zA-Z0-9_]{82}"#)
    ]

    static func containsSecret(_ value: String) -> Bool {
        let range = NSRange(value.startIndex..<value.endIndex, in: value)
        return expressions.contains { expression in
            expression.firstMatch(in: value, range: range) != nil
        }
    }

    static func masked(_ value: String) -> String {
        String(value.prefix(12)) + "************"
    }
}

enum PathRisk {
    static func isHomeRoot(_ value: String) -> Bool {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        let normalized = trimmed.replacingOccurrences(of: "\\", with: "/")
        let lower = normalized.lowercased()

        if lower == "~" || lower == "~/" {
            return true
        }

        if lower == "%userprofile%" || lower == "%userprofile%/" ||
            lower == "%homepath%" || lower == "%homepath%/" {
            return true
        }

        if lower == "/users" || lower == "/users/" {
            return true
        }

        if lower.hasPrefix("/users/") {
            let components = lower
                .split(separator: "/", omittingEmptySubsequences: true)
                .map(String.init)
            return components.count <= 2
        }

        if lower.hasPrefix("c:/users/") {
            let components = lower
                .split(separator: "/", omittingEmptySubsequences: true)
                .map(String.init)
            return components.count <= 3
        }

        return false
    }

    static func isBroadFilesystemPath(_ value: String) -> Bool {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        let normalized = trimmed.replacingOccurrences(of: "\\", with: "/")
        let lower = normalized.lowercased()

        if isHomeRoot(trimmed) {
            return false
        }

        let broadHomeDirs = [
            "~/documents",
            "~/desktop",
            "~/downloads",
            "%userprofile%/documents",
            "%userprofile%/desktop",
            "%userprofile%/downloads"
        ]
        if broadHomeDirs.contains(lower.trimmingCharacters(in: CharacterSet(charactersIn: "/"))) {
            return true
        }

        if lower.hasPrefix("/users/") {
            let components = lower
                .split(separator: "/", omittingEmptySubsequences: true)
                .map(String.init)
            return components.count <= 3
        }

        if lower.hasPrefix("c:/users/") {
            let components = lower
                .split(separator: "/", omittingEmptySubsequences: true)
                .map(String.init)
            return components.count <= 4
        }

        return false
    }
}

enum PackageVersion {
    private static let pinnedVersion = try! NSRegularExpression(
        pattern: #"@\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$"#
    )

    static func isPackageRunner(_ command: String) -> Bool {
        ["npx", "uvx", "pipx"].contains(command.lowercased())
    }

    static func hasPinnedVersion(args: [String]) -> Bool {
        args.contains { arg in
            let range = NSRange(arg.startIndex..<arg.endIndex, in: arg)
            return pinnedVersion.firstMatch(in: arg, range: range) != nil
        }
    }
}
