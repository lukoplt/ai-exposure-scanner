using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Parsing;

namespace AIExposureScanner.Scanner.Detectors;

public sealed class VSCodeDetector : IDetector
{
    public string Id => "vscode";
    public string DisplayName => "VS Code";

    public ScanFacts CollectFacts(IFilesystemFacade fs)
    {
        var facts = new ScanFacts();
        DetectorSupport.AppendAppInstallationFact(
            facts,
            fs,
            Id,
            [Path.Combine(fs.LocalApplicationDataDirectory, "Programs", "Microsoft VS Code")]
        );

        var extensionsPath = DetectorSupport.HomePath(fs, ".vscode/extensions");
        if (fs.DirectoryExists(extensionsPath))
        {
            facts.Extensions.AddRange(ParseExtensions(fs.ListDirectoryNames(extensionsPath)));
        }

        var continuePath = DetectorSupport.HomePath(fs, ".continue/config.json");
        var continueText = DetectorSupport.ReadIfPresent(fs, continuePath);
        if (continueText is not null)
        {
            facts.McpServers.AddRange(FactParsers.ParseMcpServersJson(continueText, Id, continuePath));
        }

        var storageRoot = Path.Combine(fs.ApplicationDataDirectory, "Code", "User", "globalStorage");
        foreach (var extensionDir in new[] { "saoudrizwan.claude-dev", "rooveterinaryinc.roo-cline" })
        {
            var root = Path.Combine(storageRoot, extensionDir);
            if (!fs.DirectoryExists(root))
            {
                continue;
            }

            foreach (var path in fs.ListFilesRecursively(root, 3).Where(IsMcpJsonFile))
            {
                var text = DetectorSupport.ReadIfPresent(fs, path);
                if (text is not null)
                {
                    facts.McpServers.AddRange(FactParsers.ParseMcpServersJson(text, Id, path));
                }
            }
        }

        return facts;
    }

    private IEnumerable<ExtensionFact> ParseExtensions(IEnumerable<string> folders)
    {
        foreach (var folder in folders)
        {
            if (folder.StartsWith("saoudrizwan.claude-dev-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(Id, "saoudrizwan.claude-dev", "Cline", true);
            }
            else if (folder.StartsWith("rooveterinaryinc.roo-cline-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(Id, "rooveterinaryinc.roo-cline", "Roo", true);
            }
            else if (folder.StartsWith("continue.continue-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(Id, "continue.continue", "Continue", true);
            }
            else if (folder.StartsWith("github.copilot-", StringComparison.Ordinal))
            {
                yield return new ExtensionFact(Id, "github.copilot", "GitHub Copilot", false);
            }
        }
    }

    private static bool IsMcpJsonFile(string path)
    {
        var lower = path.ToLowerInvariant();
        return lower.EndsWith(".json", StringComparison.Ordinal) && lower.Contains("mcp", StringComparison.Ordinal);
    }
}
