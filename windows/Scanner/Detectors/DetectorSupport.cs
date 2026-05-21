using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Detectors;

internal static class DetectorSupport
{
    public static string HomePath(IFilesystemFacade fs, string suffix) =>
        string.IsNullOrEmpty(suffix)
            ? fs.HomeDirectory
            : Path.Combine(new[] { fs.HomeDirectory }.Concat(suffix.Split(['/', '\\'])).ToArray());

    public static void AppendConfigFileFact(ScanFacts facts, IFilesystemFacade fs, string appId, string path)
    {
        if (fs.FileExists(path))
        {
            facts.ConfigFiles.Add(new ConfigFileFact(appId, path, fs.IsWorldReadableFile(path)));
        }
    }

    public static void AppendAppInstallationFact(
        ScanFacts facts,
        IFilesystemFacade fs,
        string appId,
        IEnumerable<string> candidatePaths
    )
    {
        var evidencePath = candidatePaths.FirstOrDefault(fs.DirectoryExists);
        facts.AppInstallations.Add(
            evidencePath is null
                ? new AppInstallationFact(appId, false)
                : new AppInstallationFact(appId, true, evidencePath)
        );
    }

    public static string? ReadIfPresent(IFilesystemFacade fs, string path) =>
        fs.FileExists(path) ? fs.ReadTextFile(path) : null;
}
