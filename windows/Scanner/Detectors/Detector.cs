using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Detectors;

public interface IDetector
{
    string Id { get; }
    string DisplayName { get; }

    ScanFacts CollectFacts(IFilesystemFacade fs);
}
