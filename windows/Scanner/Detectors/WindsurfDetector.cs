using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class WindsurfDetector : IDetector
{
    public string Id => "windsurf";
    public string DisplayName => "Windsurf";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        var mcpPath = DetectorSupport.HomePath(fs, ".codeium/windsurf/mcp_config.json");
        var settingsPath = Path.Combine(fs.ApplicationDataDirectory, "Windsurf", "User", "settings.json");
        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [Path.Combine(fs.LocalApplicationDataDirectory, "Programs", "Windsurf")]
        );

        var text = DetectorSupport.ReadIfPresent(fs, mcpPath);
        if (text is not null)
        {
            facts.McpServers.AddRange(FactParsers.ParseMcpServersJson(text, Id, mcpPath));
            DetectorSupport.AppendConfigFileFact(facts, fs, Id, mcpPath);
        }

        DetectorSupport.AppendConfigFileFact(facts, fs, Id, settingsPath);
        return facts;
    }
}
