using System.Text.RegularExpressions;
using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Rules;

internal sealed record RuleText(string Title, string Explanation, string Recommendation);

internal static class RuleTexts
{
    public static readonly IReadOnlyDictionary<string, RuleText> ById = new Dictionary<string, RuleText>
    {
        ["AES-MCP-001"] = new(
            "MCP server has broad home directory access",
            "The MCP server can read from a broad home directory scope.",
            "Restrict the server to a specific project folder."
        ),
        ["AES-MCP-002"] = new(
            "MCP server allows shell command execution",
            "The MCP server can run shell commands on this machine.",
            "Remove this server or isolate it in a sandboxed environment."
        ),
        ["AES-AUTH-001"] = new(
            "API key in plain text in MCP server env",
            "A plain text API key is stored in configuration.",
            "Move the key to the OS keychain or a supported secrets manager."
        ),
        ["AES-AUTH-002"] = new(
            "API key in plain text in MCP server args",
            "A plain text API key is passed on the command line.",
            "Never pass API keys as command-line arguments."
        ),
        ["AES-MCP-003"] = new(
            "MCP server has filesystem access to a broad path",
            "The MCP server can access a broad filesystem location.",
            "Restrict access to the narrow project folder required for the workflow."
        ),
        ["AES-MCP-004"] = new(
            "MCP server has network access",
            "The MCP server can fetch remote content or send data over the network.",
            "Disable network-capable MCP servers unless they are required."
        ),
        ["AES-MCP-005"] = new(
            "MCP server reads browser data",
            "The MCP server can automate or inspect browser sessions.",
            "Disable browser automation servers when they are not actively needed."
        ),
        ["AES-EXT-001"] = new(
            "AI coding extension can access terminal",
            "The extension can execute commands through the VS Code terminal.",
            "Disable the extension in sensitive workspaces when not in use."
        ),
        ["AES-CFG-001"] = new(
            "Cursor Privacy Mode disabled",
            "Cursor may send workspace code for AI processing.",
            "Enable Cursor Privacy Mode."
        ),
        ["AES-MCP-006"] = new(
            "Unused MCP server still configured",
            "A disabled or orphaned MCP server remains in configuration.",
            "Remove stale MCP server entries."
        ),
        ["AES-MCP-007"] = new(
            "MCP server runs without pinned version",
            "The MCP server package can change between runs.",
            "Pin the MCP server package to an exact version."
        ),
        ["AES-AUTH-003"] = new(
            "Auth token file present in plain text",
            "A local auth token file exists in a known AI tool location.",
            "Review whether the token is still needed and keep it outside broad MCP scopes."
        ),
        ["AES-CFG-002"] = new(
            "Overlapping MCP servers across clients",
            "The same MCP server name is configured in multiple AI clients.",
            "Consolidate duplicate MCP server registrations."
        ),
        ["AES-CFG-003"] = new(
            "MCP server has no description",
            "The MCP server has no documented purpose or expected access scope.",
            "Add a description explaining why this server exists."
        ),
        ["AES-CFG-004"] = new(
            "AI tool config file is world-readable",
            "A configuration file can be read by other users or broad principals.",
            "Restrict the config file permissions."
        ),
        ["AES-MCP-008"] = new(
            "MCP server overrides the AI model API base URL",
            "An environment variable redirects model API traffic to a non-official endpoint, which can route every prompt and response through a third party.",
            "Remove the base URL override unless the proxy is fully trusted, and prefer official provider endpoints."
        ),
        ["AES-MCP-009"] = new(
            "MCP server runs a remote install script",
            "The server downloads and pipes a script from the internet straight into a shell, executing unreviewed remote code on every run.",
            "Install the server from a pinned, vetted package instead of piping a network download into a shell."
        ),
        ["AES-MCP-010"] = new(
            "MCP server uses a plaintext HTTP endpoint",
            "The server talks to a remote endpoint over unencrypted HTTP, exposing traffic to interception and tampering.",
            "Switch the endpoint to HTTPS, or restrict it to a trusted local address."
        ),
        ["AES-MCP-011"] = new(
            "MCP server can access SSH or cloud credential directories",
            "The server is scoped to a directory or file holding SSH keys or cloud credentials, giving an agent a direct path to crown-jewel secrets.",
            "Re-scope the server away from credential directories such as ~/.ssh, ~/.aws, or ~/.gnupg."
        ),
        ["AES-MCP-012"] = new(
            "MCP server can access the Docker daemon socket",
            "The server can reach the Docker socket or runs privileged containers, which is equivalent to root on the host and enables container escape.",
            "Remove Docker socket access and privileged flags from this server."
        ),
        ["AES-MCP-013"] = new(
            "Messaging or email MCP server can exfiltrate data",
            "The server can send messages or email, giving a prompt-injected agent an outbound channel to leak data.",
            "Disable messaging servers when not actively needed and restrict their scopes and recipients."
        ),
        ["AES-AUTH-004"] = new(
            "Cloud provider credentials in MCP server env",
            "Long-lived cloud access keys are stored in plain text in configuration, exposing the cloud account to any agent or prompt injection.",
            "Move cloud credentials to a secrets manager or short-lived role-based credentials, never plain config."
        ),
        ["AES-AUTH-005"] = new(
            "Database connection string with credentials",
            "A database URL embeds a username and password in plain text, exposing the database to any agent that reads this configuration.",
            "Use a secrets manager or credential-free connection method instead of an inline password."
        ),
        ["AES-AUTH-006"] = new(
            "Plaintext secret in MCP server env",
            "An environment variable whose name indicates a secret holds a plain text literal value in configuration.",
            "Move the secret to the OS keychain or a secrets manager and reference it indirectly."
        )
    };
}

internal static class RuleExtensions
{
    public static Finding Finding(
        this IRule rule,
        string app,
        string? serverName = null,
        string? extensionId = null,
        string? affectedPath = null,
        string? maskedValue = null
    )
    {
        var text = RuleTexts.ById[rule.Id];
        return new Finding(
            rule.Id,
            rule.Severity,
            app,
            serverName,
            extensionId,
            affectedPath,
            maskedValue,
            text.Title,
            text.Explanation,
            text.Recommendation
        );
    }

    public static string CommandName(this McpServerFact server)
    {
        var parts = server.Command.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? server.Command : parts[^1];
    }

    public static IEnumerable<string> SearchableValues(this McpServerFact server) =>
        new[] { server.Command }.Concat(server.Args).Concat(server.Env.Values);
}

public static partial class SecretPatterns
{
    private static readonly Regex[] Expressions =
    [
        AnthropicRegex(),
        OpenAiClassicRegex(),
        OpenAiProjectRegex(),
        GoogleAiRegex(),
        GitHubClassicRegex(),
        GitHubFineGrainedRegex()
    ];

    public static bool ContainsSecret(string value) =>
        Expressions.Any(expression => expression.IsMatch(value));

    /// <summary>
    /// Returns a masked representation that reveals only the schema prefix
    /// (e.g. "sk-ant-", "AIza", "ghp_") and replaces all entropy characters
    /// with bullets. Never leaks any bytes of the secret material itself.
    /// </summary>
    public static string Masked(string value)
    {
        // Most-specific prefixes first so we never match "sk-" before "sk-ant-".
        string[] knownPrefixes = ["sk-ant-", "sk-proj-", "github_pat_", "ghp_", "AIza", "sk-"];
        foreach (var prefix in knownPrefixes)
        {
            var idx = value.IndexOf(prefix, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var before = value[..idx];
                return before + prefix + new string('•', 24);
            }
        }
        return new string('•', Math.Min(value.Length, 16));
    }

    [GeneratedRegex(@"sk-ant-[a-zA-Z0-9\-_]{80,}")]
    private static partial Regex AnthropicRegex();

    [GeneratedRegex(@"sk-[a-zA-Z0-9]{48}")]
    private static partial Regex OpenAiClassicRegex();

    [GeneratedRegex(@"sk-proj-[a-zA-Z0-9\-_]{48,}")]
    private static partial Regex OpenAiProjectRegex();

    [GeneratedRegex(@"AIza[a-zA-Z0-9\-_]{35,}")]
    private static partial Regex GoogleAiRegex();

    [GeneratedRegex(@"ghp_[a-zA-Z0-9]{36}")]
    private static partial Regex GitHubClassicRegex();

    [GeneratedRegex(@"github_pat_[a-zA-Z0-9_]{82}")]
    private static partial Regex GitHubFineGrainedRegex();
}

internal static class PathRisk
{
    public static bool IsHomeRoot(string value)
    {
        var lower = value.Trim().Replace('\\', '/').ToLowerInvariant();

        if (lower is "~" or "~/" or "%userprofile%" or "%userprofile%/" or "%homepath%" or "%homepath%/")
        {
            return true;
        }

        if (lower is "/users" or "/users/")
        {
            return true;
        }

        if (lower.StartsWith("/users/", StringComparison.Ordinal))
        {
            var components = lower.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return components.Length <= 2;
        }

        if (lower.StartsWith("c:/users/", StringComparison.Ordinal))
        {
            var components = lower.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return components.Length <= 3;
        }

        return false;
    }

    public static bool IsBroadFilesystemPath(string value)
    {
        var lower = value.Trim().Replace('\\', '/').ToLowerInvariant();

        if (IsHomeRoot(lower))
        {
            return false;
        }

        var broadHomeDirs = new HashSet<string>(StringComparer.Ordinal)
        {
            "~/documents",
            "~/desktop",
            "~/downloads",
            "%userprofile%/documents",
            "%userprofile%/desktop",
            "%userprofile%/downloads"
        };

        if (broadHomeDirs.Contains(lower.Trim('/')))
        {
            return true;
        }

        if (lower.StartsWith("/users/", StringComparison.Ordinal))
        {
            var components = lower.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return components.Length <= 3;
        }

        if (lower.StartsWith("c:/users/", StringComparison.Ordinal))
        {
            var components = lower.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return components.Length <= 4;
        }

        return false;
    }
}

internal static partial class PackageVersion
{
    public static bool IsPackageRunner(string command) =>
        command.ToLowerInvariant() is "npx" or "uvx" or "pipx";

    public static bool HasPinnedVersion(IEnumerable<string> args) =>
        args.Any(arg => PinnedVersionRegex().IsMatch(arg));

    [GeneratedRegex(@"@\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$")]
    private static partial Regex PinnedVersionRegex();
}

/// <summary>
/// Detection helpers for AI-tool-specific exposure patterns (AES-MCP-008..013, AES-AUTH-004..006).
/// Kept in lock-step with the Swift <c>AiThreatPatterns</c> so both platforms detect identically.
/// </summary>
internal static partial class AiThreatPatterns
{
    // Cloud credentials (AES-AUTH-004)
    private static readonly HashSet<string> CloudCredentialKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "AWS_ACCESS_KEY_ID",
        "AWS_SECRET_ACCESS_KEY",
        "AWS_SESSION_TOKEN",
        "AZURE_CLIENT_SECRET",
        "GOOGLE_API_KEY",
        "GCP_API_KEY",
        "GCLOUD_API_KEY",
        "DIGITALOCEAN_ACCESS_TOKEN",
        "DO_API_TOKEN"
    };

    public static bool IsCloudCredentialEnv(string key, string value) =>
        CloudCredentialKeys.Contains(key) && IsLiteralSecretValue(value);

    // Model base URL override (AES-MCP-008)
    private static readonly HashSet<string> BaseUrlKeys = new(StringComparer.OrdinalIgnoreCase)
    {
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
    };

    public static bool IsModelBaseUrlOverride(string key, string value)
    {
        if (!BaseUrlKeys.Contains(key))
        {
            return false;
        }
        var lower = value.ToLowerInvariant();
        if (!lower.Contains("http://", StringComparison.Ordinal) && !lower.Contains("https://", StringComparison.Ordinal))
        {
            return false;
        }
        return !IsLocalHost(lower);
    }

    // Database connection string (AES-AUTH-005)
    public static bool IsConnectionStringWithCredentials(string value) =>
        ConnectionStringRegex().IsMatch(value);

    [GeneratedRegex(@"\b(postgres|postgresql|mysql|mariadb|mongodb|mongodb\+srv|redis|rediss|mssql|sqlserver|amqp|amqps)://[^/\s:@]+:[^/\s@]+@", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionStringRegex();

    // Generic plaintext secret env (AES-AUTH-006)
    private static readonly string[] SecretKeySuffixes =
    [
        "_TOKEN", "_SECRET", "_PASSWORD", "_PASSWD",
        "_API_KEY", "_APIKEY", "_ACCESS_KEY", "_PRIVATE_KEY", "_CLIENT_SECRET"
    ];

    private static readonly HashSet<string> SecretKeyExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "TOKEN", "SECRET", "PASSWORD", "PASSWD", "APIKEY", "API_KEY", "ACCESS_TOKEN", "AUTH_TOKEN"
    };

    public static bool LooksLikeSecretEnvKey(string key)
    {
        var upper = key.ToUpperInvariant();
        if (SecretKeyExact.Contains(upper))
        {
            return true;
        }
        return SecretKeySuffixes.Any(suffix => upper.EndsWith(suffix, StringComparison.Ordinal));
    }

    /// <summary>
    /// True when the value is a concrete inline secret, not empty and not an
    /// indirect reference like <c>${VAR}</c>, <c>$(cmd)</c>, <c>$VAR</c>, or <c>%VAR%</c>.
    /// </summary>
    public static bool IsLiteralSecretValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 6)
        {
            return false;
        }
        if (trimmed.StartsWith("${", StringComparison.Ordinal) || trimmed.StartsWith("$(", StringComparison.Ordinal))
        {
            return false;
        }
        if (trimmed.StartsWith('$'))
        {
            return false;
        }
        if (trimmed.StartsWith('%') && trimmed.EndsWith('%'))
        {
            return false;
        }
        return true;
    }

    // Supply-chain remote install (AES-MCP-009)
    public static bool IsRemoteInstallScript(string command, IEnumerable<string> args)
    {
        var joined = string.Join(' ', new[] { command }.Concat(args)).ToLowerInvariant();
        var downloadsAndPipesToShell =
            (joined.Contains("curl", StringComparison.Ordinal) || joined.Contains("wget", StringComparison.Ordinal)) &&
            (joined.Contains("| sh", StringComparison.Ordinal) || joined.Contains("|sh", StringComparison.Ordinal) ||
             joined.Contains("| bash", StringComparison.Ordinal) || joined.Contains("|bash", StringComparison.Ordinal) ||
             joined.Contains("| zsh", StringComparison.Ordinal) || joined.Contains("|zsh", StringComparison.Ordinal));
        var powershellDownloadExec =
            joined.Contains("iex", StringComparison.Ordinal) &&
            (joined.Contains("irm", StringComparison.Ordinal) || joined.Contains("iwr", StringComparison.Ordinal) ||
             joined.Contains("invoke-webrequest", StringComparison.Ordinal) || joined.Contains("invoke-restmethod", StringComparison.Ordinal));
        return downloadsAndPipesToShell || powershellDownloadExec;
    }

    // Plaintext HTTP endpoint (AES-MCP-010)
    public static bool IsPlaintextHttpEndpoint(string value)
    {
        var lower = value.ToLowerInvariant();
        var idx = lower.IndexOf("http://", StringComparison.Ordinal);
        if (idx < 0)
        {
            return false;
        }
        var rest = lower[(idx + "http://".Length)..];
        var host = new string(rest.TakeWhile(c => c != '/' && c != ':' && c != ' ' && c != '"').ToArray());
        return !IsLocalHostName(host);
    }

    // Sensitive credential paths (AES-MCP-011)
    private static readonly string[] SensitivePathMarkers =
    [
        "/.ssh", "~/.ssh", ".ssh/",
        "/.aws", "~/.aws", ".aws/",
        "/.gnupg", ".gnupg",
        "/.kube", ".kube/",
        ".config/gcloud",
        ".docker/config.json",
        "/.netrc", ".netrc",
        "id_rsa", "id_ed25519"
    ];

    public static bool ContainsSensitiveCredentialPath(string value)
    {
        var lower = value.Replace('\\', '/').ToLowerInvariant();
        return SensitivePathMarkers.Any(marker => lower.Contains(marker, StringComparison.Ordinal));
    }

    // Docker daemon socket (AES-MCP-012)
    public static bool GrantsDockerDaemonAccess(string value)
    {
        var lower = value.ToLowerInvariant();
        return lower.Contains("docker.sock", StringComparison.Ordinal) || lower == "--privileged";
    }

    // Messaging / email exfiltration channel (AES-MCP-013)
    private static readonly string[] MessagingMarkers =
    [
        "server-slack", "slack-mcp", "mcp-slack", "discord", "telegram",
        "mattermost", "server-gmail", "gmail-mcp", "sendgrid", "mailgun",
        "twilio", "whatsapp", "server-email"
    ];

    public static bool IsMessagingServer(IEnumerable<string> values)
    {
        var lowered = values.Select(v => v.ToLowerInvariant()).ToArray();
        return lowered.Any(value => MessagingMarkers.Any(marker => value.Contains(marker, StringComparison.Ordinal)));
    }

    // Shared
    private static bool IsLocalHost(string lowerValue) =>
        lowerValue.Contains("localhost", StringComparison.Ordinal) ||
        lowerValue.Contains("127.0.0.1", StringComparison.Ordinal) ||
        lowerValue.Contains("0.0.0.0", StringComparison.Ordinal) ||
        lowerValue.Contains("[::1]", StringComparison.Ordinal) ||
        lowerValue.Contains("host.docker.internal", StringComparison.Ordinal);

    private static bool IsLocalHostName(string host) =>
        host is "localhost" or "127.0.0.1" or "0.0.0.0" or "::1" or "host.docker.internal" ||
        host.StartsWith("192.168.", StringComparison.Ordinal) ||
        host.StartsWith("10.", StringComparison.Ordinal) ||
        host.StartsWith("172.", StringComparison.Ordinal);
}
