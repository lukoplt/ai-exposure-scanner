namespace AIExposureScanner.Scanner.Models;

public sealed record McpServerFact(
    string AppId,
    string Name,
    string Command,
    IReadOnlyList<string> Args,
    IReadOnlyDictionary<string, string> Env,
    bool Disabled = false,
    string? Description = null,
    string? ConfigPath = null
);

public sealed record SettingFact(
    string AppId,
    string Key,
    bool? BoolValue = null,
    string? StringValue = null,
    string? ConfigPath = null
);

public sealed record AuthFileFact(string AppId, string FilePath);

public sealed record ExtensionFact(
    string AppId,
    string ExtensionId,
    string ExtensionName,
    bool HasTerminalAccess
);

public sealed record ConfigFileFact(string AppId, string Path, bool WorldReadable);

public sealed record AppInstallationFact(string AppId, bool Installed, string? EvidencePath = null);

public sealed class ScanFacts
{
    public List<McpServerFact> McpServers { get; } = [];
    public List<SettingFact> Settings { get; } = [];
    public List<AuthFileFact> AuthFiles { get; } = [];
    public List<ExtensionFact> Extensions { get; } = [];
    public List<ConfigFileFact> ConfigFiles { get; } = [];
    public List<AppInstallationFact> AppInstallations { get; } = [];

    public void Append(ScanFacts other)
    {
        McpServers.AddRange(other.McpServers);
        Settings.AddRange(other.Settings);
        AuthFiles.AddRange(other.AuthFiles);
        Extensions.AddRange(other.Extensions);
        ConfigFiles.AddRange(other.ConfigFiles);
        AppInstallations.AddRange(other.AppInstallations);
    }
}
