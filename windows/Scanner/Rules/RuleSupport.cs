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

internal static partial class SecretPatterns
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

    public static string Masked(string value) =>
        value[..Math.Min(12, value.Length)] + "************";

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
