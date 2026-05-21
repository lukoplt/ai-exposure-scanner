using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class ClaudeDesktopDetector : IDetector
{
    public string Id => "claude-desktop";
    public string DisplayName => "Claude Desktop";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        var configPath = Path.Combine(fs.ApplicationDataDirectory, "Claude", "claude_desktop_config.json");

        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [Path.Combine(fs.LocalApplicationDataDirectory, "AnthropicClaude")]
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
