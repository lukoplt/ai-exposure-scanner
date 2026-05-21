using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner;

public interface IRule
{
    string Id { get; }
    Severity Severity { get; }
    IReadOnlyList<Finding> Evaluate(ScanFacts facts);
}
