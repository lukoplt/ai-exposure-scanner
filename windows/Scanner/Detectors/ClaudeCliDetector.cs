using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class ClaudeCliDetector : IDetector
{
    public string Id => "claude-cli";
    public string DisplayName => "Claude CLI";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        // Global config: ~/.claude.json (macOS/Linux) or %USERPROFILE%\.claude.json (Windows)
        var configPath = DetectorSupport.HomePath(fs, ".claude.json");

        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [DetectorSupport.HomePath(fs, ".claude")]
        );

        var text = DetectorSupport.ReadIfPresent(fs, configPath);
        if (text is not null)
        {
            facts.McpServers.AddRange(FactParsers.ParseMcpServersJson(text, Id, configPath));
            DetectorSupport.AppendConfigFileFact(facts, fs, Id, configPath);
        }

        return facts;
    }
}
