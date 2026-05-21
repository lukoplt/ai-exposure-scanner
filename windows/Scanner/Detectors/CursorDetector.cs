using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class CursorDetector : IDetector
{
    public string Id => "cursor";
    public string DisplayName => "Cursor";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        var mcpPath = DetectorSupport.HomePath(fs, ".cursor/mcp.json");
        var settingsPath = Path.Combine(fs.ApplicationDataDirectory, "Cursor", "User", "settings.json");
        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [Path.Combine(fs.LocalApplicationDataDirectory, "Programs", "cursor")]
        );

        var mcpText = DetectorSupport.ReadIfPresent(fs, mcpPath);
        if (mcpText is not null)
        {
            facts.McpServers.AddRange(FactParsers.ParseMcpServersJson(mcpText, Id, mcpPath));
            DetectorSupport.AppendConfigFileFact(facts, fs, Id, mcpPath);
        }

        var settingsText = DetectorSupport.ReadIfPresent(fs, settingsPath);
        if (settingsText is not null)
        {
            facts.Settings.Add(FactParsers.ParseCursorPrivacyMode(settingsText, Id, settingsPath));
            DetectorSupport.AppendConfigFileFact(facts, fs, Id, settingsPath);
        }

        return facts;
    }
}
