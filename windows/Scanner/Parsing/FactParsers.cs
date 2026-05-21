using System.Text.Json;
using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Parsing;

internal static class FactParsers
{
    public static IReadOnlyList<McpServerFact> ParseMcpServersJson(string text, string appId, string configPath)
    {
        using var document = JsonDocument.Parse(text);
        return document.RootElement.TryGetProperty("mcpServers", out var servers)
            ? ParseMcpServers(servers, appId, configPath).ToArray()
            : [];
    }

    public static IEnumerable<McpServerFact> ParseMcpServers(JsonElement servers, string appId, string configPath)
    {
        foreach (var server in servers.EnumerateObject())
        {
            var config = server.Value;
            var args = config.TryGetProperty("args", out var argsElement) && argsElement.ValueKind == JsonValueKind.Array
                ? argsElement.EnumerateArray().Select(arg => arg.GetString() ?? string.Empty).ToArray()
                : [];
            var env = config.TryGetProperty("env", out var envElement) && envElement.ValueKind == JsonValueKind.Object
                ? envElement.EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString() ?? string.Empty)
                : new Dictionary<string, string>();

            yield return new McpServerFact(
                appId,
                server.Name,
                config.TryGetProperty("command", out var command) ? command.GetString() ?? string.Empty : string.Empty,
                args,
                env,
                config.TryGetProperty("disabled", out var disabled) && disabled.GetBoolean(),
                config.TryGetProperty("description", out var description) ? description.GetString() : null,
                configPath
            );
        }
    }

    public static SettingFact ParseCursorPrivacyMode(string text, string appId, string configPath)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var boolValue = root.TryGetProperty("cursor.privacyMode", out var privacyMode) && privacyMode.ValueKind == JsonValueKind.True
            ? true
            : root.TryGetProperty("cursor.privacyMode", out privacyMode) && privacyMode.ValueKind == JsonValueKind.False
                ? false
                : (bool?)null;
        return new SettingFact(appId, "cursor.privacyMode", boolValue, ConfigPath: configPath);
    }

    public static ScanFacts ParseGeminiSettings(string text, string appId, string configPath)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        var facts = new ScanFacts();

        if (root.TryGetProperty("mcpServers", out var servers))
        {
            facts.McpServers.AddRange(ParseMcpServers(servers, appId, configPath));
        }
        if (root.TryGetProperty("apiKey", out var apiKey))
        {
            facts.Settings.Add(new SettingFact(appId, "apiKey", StringValue: apiKey.GetString(), ConfigPath: configPath));
        }

        return facts;
    }

    public static IReadOnlyList<McpServerFact> ParseCodexToml(string text, string appId, string configPath)
    {
        var servers = new Dictionary<string, MutableMcpServer>(StringComparer.Ordinal);
        string? currentServer = null;
        string? currentEnvServer = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1];
                if (section.StartsWith("mcp_servers.", StringComparison.Ordinal))
                {
                    var suffix = section["mcp_servers.".Length..];
                    if (suffix.EndsWith(".env", StringComparison.Ordinal))
                    {
                        var name = suffix[..^".env".Length];
                        currentServer = name;
                        currentEnvServer = name;
                        GetServer(servers, name).Name = name;
                    }
                    else
                    {
                        currentServer = suffix;
                        currentEnvServer = null;
                        GetServer(servers, suffix).Name = suffix;
                    }
                }
                else
                {
                    currentServer = null;
                    currentEnvServer = null;
                }
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (currentEnvServer is not null)
            {
                GetServer(servers, currentEnvServer).Env[key] = ParseTomlString(value);
            }
            else if (currentServer is not null)
            {
                var server = GetServer(servers, currentServer);
                switch (key)
                {
                    case "command":
                        server.Command = ParseTomlString(value);
                        break;
                    case "args":
                        server.Args = ParseTomlStringArray(value);
                        break;
                    case "disabled":
                        server.Disabled = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "description":
                        server.Description = ParseTomlString(value);
                        break;
                }
            }
        }

        return servers.Values
            .Select(server => new McpServerFact(
                appId,
                server.Name,
                server.Command,
                server.Args,
                server.Env,
                server.Disabled,
                server.Description,
                configPath
            ))
            .ToArray();
    }

    private static MutableMcpServer GetServer(Dictionary<string, MutableMcpServer> servers, string name)
    {
        if (!servers.TryGetValue(name, out var server))
        {
            server = new MutableMcpServer { Name = name };
            servers[name] = server;
        }
        return server;
    }

    private static string ParseTomlString(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal)
            : trimmed;
    }

    private static IReadOnlyList<string> ParseTomlStringArray(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            return [];
        }

        var inner = trimmed[1..^1];
        var result = new List<string>();
        var current = "";
        var inString = false;
        var escaped = false;

        foreach (var character in inner)
        {
            if (escaped)
            {
                current += character;
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == '"')
            {
                if (inString)
                {
                    result.Add(current);
                    current = "";
                }
                inString = !inString;
            }
            else if (inString)
            {
                current += character;
            }
        }

        return result;
    }

    private sealed class MutableMcpServer
    {
        public string Name { get; set; } = "";
        public string Command { get; set; } = "";
        public IReadOnlyList<string> Args { get; set; } = [];
        public Dictionary<string, string> Env { get; } = [];
        public bool Disabled { get; set; }
        public string? Description { get; set; }
    }
}
