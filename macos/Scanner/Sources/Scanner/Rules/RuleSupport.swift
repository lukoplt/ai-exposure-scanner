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
        ),
        "AES-MCP-008": RuleText(
            title: "MCP server overrides the AI model API base URL",
            explanation: "An environment variable redirects model API traffic to a non-official endpoint, which can route every prompt and response through a third party.",
            recommendation: "Remove the base URL override unless the proxy is fully trusted, and prefer official provider endpoints."
        ),
        "AES-MCP-009": RuleText(
            title: "MCP server runs a remote install script",
            explanation: "The server downloads and pipes a script from the internet straight into a shell, executing unreviewed remote code on every run.",
            recommendation: "Install the server from a pinned, vetted package instead of piping a network download into a shell."
        ),
        "AES-MCP-010": RuleText(
            title: "MCP server uses a plaintext HTTP endpoint",
            explanation: "The server talks to a remote endpoint over unencrypted HTTP, exposing traffic to interception and tampering.",
            recommendation: "Switch the endpoint to HTTPS, or restrict it to a trusted local address."
        ),
        "AES-MCP-011": RuleText(
            title: "MCP server can access SSH or cloud credential directories",
            explanation: "The server is scoped to a directory or file holding SSH keys or cloud credentials, giving an agent a direct path to crown-jewel secrets.",
            recommendation: "Re-scope the server away from credential directories such as ~/.ssh, ~/.aws, or ~/.gnupg."
        ),
        "AES-MCP-012": RuleText(
            title: "MCP server can access the Docker daemon socket",
            explanation: "The server can reach the Docker socket or runs privileged containers, which is equivalent to root on the host and enables container escape.",
            recommendation: "Remove Docker socket access and privileged flags from this server."
        ),
        "AES-MCP-013": RuleText(
            title: "Messaging or email MCP server can exfiltrate data",
            explanation: "The server can send messages or email, giving a prompt-injected agent an outbound channel to leak data.",
            recommendation: "Disable messaging servers when not actively needed and restrict their scopes and recipients."
        ),
        "AES-AUTH-004": RuleText(
            title: "Cloud provider credentials in MCP server env",
            explanation: "Long-lived cloud access keys are stored in plain text in configuration, exposing the cloud account to any agent or prompt injection.",
            recommendation: "Move cloud credentials to a secrets manager or short-lived role-based credentials, never plain config."
        ),
        "AES-AUTH-005": RuleText(
            title: "Database connection string with credentials",
            explanation: "A database URL embeds a username and password in plain text, exposing the database to any agent that reads this configuration.",
            recommendation: "Use a secrets manager or credential-free connection method instead of an inline password."
        ),
        "AES-AUTH-006": RuleText(
            title: "Plaintext secret in MCP server env",
            explanation: "An environment variable whose name indicates a secret holds a plain text literal value in configuration.",
            recommendation: "Move the secret to the OS keychain or a secrets manager and reference it indirectly."
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

public enum SecretPatterns {
    private static let expressions: [NSRegularExpression] = [
        try! NSRegularExpression(pattern: #"sk-ant-[a-zA-Z0-9\-_]{80,}"#),
        try! NSRegularExpression(pattern: #"sk-[a-zA-Z0-9]{48}"#),
        try! NSRegularExpression(pattern: #"sk-proj-[a-zA-Z0-9\-_]{48,}"#),
        try! NSRegularExpression(pattern: #"AIza[a-zA-Z0-9\-_]{35,}"#),
        try! NSRegularExpression(pattern: #"ghp_[a-zA-Z0-9]{36}"#),
        try! NSRegularExpression(pattern: #"github_pat_[a-zA-Z0-9_]{82}"#)
    ]

    public static func containsSecret(_ value: String) -> Bool {
        let range = NSRange(value.startIndex..<value.endIndex, in: value)
        return expressions.contains { expression in
            expression.firstMatch(in: value, range: range) != nil
        }
    }

    /// Returns a masked representation that reveals only the schema prefix
    /// (e.g. "sk-ant-", "AIza", "ghp_") and replaces all entropy characters
    /// with bullets. Never leaks any bytes of the secret material itself.
    static func masked(_ value: String) -> String {
        // Most-specific prefixes first so we never match "sk-" before "sk-ant-".
        let knownPrefixes = ["sk-ant-", "sk-proj-", "github_pat_", "ghp_", "AIza", "sk-"]
        for prefix in knownPrefixes {
            if let range = value.range(of: prefix) {
                let before = String(value[..<range.lowerBound])
                return before + prefix + String(repeating: "•", count: 24)
            }
        }
        return String(repeating: "•", count: min(value.count, 16))
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

/// Detection helpers for AI-tool-specific exposure patterns (AES-MCP-008..013, AES-AUTH-004..006).
/// Kept in lock-step with the C# `AiThreatPatterns` so both platforms detect identically.
enum AiThreatPatterns {
    // MARK: Cloud credentials (AES-AUTH-004)

    static let cloudCredentialKeys: Set<String> = [
        "AWS_ACCESS_KEY_ID",
        "AWS_SECRET_ACCESS_KEY",
        "AWS_SESSION_TOKEN",
        "AZURE_CLIENT_SECRET",
        "GOOGLE_API_KEY",
        "GCP_API_KEY",
        "GCLOUD_API_KEY",
        "DIGITALOCEAN_ACCESS_TOKEN",
        "DO_API_TOKEN"
    ]

    static func isCloudCredentialEnv(key: String, value: String) -> Bool {
        cloudCredentialKeys.contains(key.uppercased()) && isLiteralSecretValue(value)
    }

    // MARK: Model base URL override (AES-MCP-008)

    static let baseUrlKeys: Set<String> = [
        "OPENAI_BASE_URL",
        "OPENAI_API_BASE",
        "OPENAI_API_BASE_URL",
        "ANTHROPIC_BASE_URL",
        "ANTHROPIC_API_URL",
        "AZURE_OPENAI_ENDPOINT",
        "COHERE_BASE_URL",
        "GROQ_BASE_URL",
        "MISTRAL_BASE_URL",
        "LLM_BASE_URL",
        "API_BASE_URL",
        "OPENAI_BASE"
    ]

    static func isModelBaseUrlOverride(key: String, value: String) -> Bool {
        guard baseUrlKeys.contains(key.uppercased()) else { return false }
        let lower = value.lowercased()
        guard lower.contains("http://") || lower.contains("https://") else { return false }
        return !isLocalHost(lower)
    }

    // MARK: Database connection string (AES-AUTH-005)

    private static let connectionString = try! NSRegularExpression(
        pattern: #"(?i)\b(postgres|postgresql|mysql|mariadb|mongodb|mongodb\+srv|redis|rediss|mssql|sqlserver|amqp|amqps)://[^/\s:@]+:[^/\s@]+@"#
    )

    static func isConnectionStringWithCredentials(_ value: String) -> Bool {
        matches(connectionString, value)
    }

    // MARK: Generic plaintext secret env (AES-AUTH-006)

    private static let secretKeySuffixes = [
        "_TOKEN", "_SECRET", "_PASSWORD", "_PASSWD",
        "_API_KEY", "_APIKEY", "_ACCESS_KEY", "_PRIVATE_KEY", "_CLIENT_SECRET"
    ]
    private static let secretKeyExact: Set<String> = [
        "TOKEN", "SECRET", "PASSWORD", "PASSWD", "APIKEY", "API_KEY", "ACCESS_TOKEN", "AUTH_TOKEN"
    ]

    static func looksLikeSecretEnvKey(_ key: String) -> Bool {
        let upper = key.uppercased()
        if secretKeyExact.contains(upper) { return true }
        return secretKeySuffixes.contains { upper.hasSuffix($0) }
    }

    /// True when the value is a concrete inline secret, not empty and not an
    /// indirect reference like `${VAR}`, `$(cmd)`, `$VAR`, or `%VAR%`.
    static func isLiteralSecretValue(_ value: String) -> Bool {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.count >= 6 else { return false }
        if trimmed.hasPrefix("${") || trimmed.hasPrefix("$(") { return false }
        if trimmed.hasPrefix("$") { return false }
        if trimmed.hasPrefix("%") && trimmed.hasSuffix("%") { return false }
        return true
    }

    // MARK: Supply-chain remote install (AES-MCP-009)

    static func isRemoteInstallScript(command: String, args: [String]) -> Bool {
        let joined = ([command] + args).joined(separator: " ").lowercased()
        let downloadsAndPipesToShell =
            (joined.contains("curl") || joined.contains("wget")) &&
            (joined.contains("| sh") || joined.contains("|sh") ||
             joined.contains("| bash") || joined.contains("|bash") ||
             joined.contains("| zsh") || joined.contains("|zsh"))
        let powershellDownloadExec =
            joined.contains("iex") &&
            (joined.contains("irm") || joined.contains("iwr") ||
             joined.contains("invoke-webrequest") || joined.contains("invoke-restmethod"))
        return downloadsAndPipesToShell || powershellDownloadExec
    }

    // MARK: Plaintext HTTP endpoint (AES-MCP-010)

    static func isPlaintextHttpEndpoint(_ value: String) -> Bool {
        let lower = value.lowercased()
        guard let range = lower.range(of: "http://") else { return false }
        let host = lower[range.upperBound...].prefix { $0 != "/" && $0 != ":" && $0 != " " && $0 != "\"" }
        return !isLocalHostName(String(host))
    }

    // MARK: Sensitive credential paths (AES-MCP-011)

    private static let sensitivePathMarkers = [
        "/.ssh", "~/.ssh", ".ssh/",
        "/.aws", "~/.aws", ".aws/",
        "/.gnupg", ".gnupg",
        "/.kube", ".kube/",
        ".config/gcloud",
        ".docker/config.json",
        "/.netrc", ".netrc",
        "id_rsa", "id_ed25519"
    ]

    static func containsSensitiveCredentialPath(_ value: String) -> Bool {
        let lower = value.replacingOccurrences(of: "\\", with: "/").lowercased()
        return sensitivePathMarkers.contains { lower.contains($0) }
    }

    // MARK: Docker daemon socket (AES-MCP-012)

    static func grantsDockerDaemonAccess(_ value: String) -> Bool {
        let lower = value.lowercased()
        return lower.contains("docker.sock") || lower == "--privileged"
    }

    // MARK: Messaging / email exfiltration channel (AES-MCP-013)

    private static let messagingMarkers = [
        "server-slack", "slack-mcp", "mcp-slack", "discord", "telegram",
        "mattermost", "server-gmail", "gmail-mcp", "sendgrid", "mailgun",
        "twilio", "whatsapp", "server-email"
    ]

    static func isMessagingServer(values: [String]) -> Bool {
        let lowered = values.map { $0.lowercased() }
        return lowered.contains { value in messagingMarkers.contains(where: value.contains) }
    }

    // MARK: Shared

    private static func isLocalHost(_ lowerValue: String) -> Bool {
        lowerValue.contains("localhost") ||
            lowerValue.contains("127.0.0.1") ||
            lowerValue.contains("0.0.0.0") ||
            lowerValue.contains("[::1]") ||
            lowerValue.contains("host.docker.internal")
    }

    private static func isLocalHostName(_ host: String) -> Bool {
        host == "localhost" || host == "127.0.0.1" || host == "0.0.0.0" ||
            host == "::1" || host == "host.docker.internal" || host.hasPrefix("192.168.") ||
            host.hasPrefix("10.") || host.hasPrefix("172.")
    }

    private static func matches(_ expression: NSRegularExpression, _ value: String) -> Bool {
        let range = NSRange(value.startIndex..<value.endIndex, in: value)
        return expression.firstMatch(in: value, range: range) != nil
    }
}
