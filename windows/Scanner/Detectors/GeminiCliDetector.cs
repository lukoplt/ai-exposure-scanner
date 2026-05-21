using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class GeminiCliDetector : IDetector
{
    public string Id => "gemini-cli";
    public string DisplayName => "Gemini CLI";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        var settingsPath = DetectorSupport.HomePath(fs, ".gemini/settings.json");
        var credentialsPath = DetectorSupport.HomePath(fs, ".gemini/credentials");
        var oauthPath = DetectorSupport.HomePath(fs, ".gemini/oauth_credentials.json");
        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [DetectorSupport.HomePath(fs, ".gemini")]
        );

        var text = DetectorSupport.ReadIfPresent(fs, settingsPath);
        if (text is not null)
        {
            facts.Append(FactParsers.ParseGeminiSettings(text, Id, settingsPath));
            DetectorSupport.AppendConfigFileFact(facts, fs, Id, settingsPath);
        }

        if (fs.FileExists(credentialsPath))
        {
            facts.AuthFiles.Add(new AuthFileFact(Id, credentialsPath));
        }
        if (fs.FileExists(oauthPath))
        {
            facts.AuthFiles.Add(new AuthFileFact(Id, oauthPath));
        }

        return facts;
    }
}
