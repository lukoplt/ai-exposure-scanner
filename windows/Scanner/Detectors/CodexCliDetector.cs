using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class CodexCliDetector : IDetector
{
    public string Id => "codex-cli";
    public string DisplayName => "Codex CLI";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        var configPath = DetectorSupport.HomePath(fs, ".codex/config.toml");
        var authPath = DetectorSupport.HomePath(fs, ".codex/auth.json");
        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [DetectorSupport.HomePath(fs, ".codex")]
        );

        var text = DetectorSupport.ReadIfPresent(fs, configPath);
        if (text is not null)
        {
            facts.McpServers.AddRange(FactParsers.ParseCodexToml(text, Id, configPath));
            DetectorSupport.AppendConfigFileFact(facts, fs, Id, configPath);
        }

        if (fs.FileExists(authPath))
        {
            facts.AuthFiles.Add(new AuthFileFact(Id, authPath));
        }

        return facts;
    }
}
