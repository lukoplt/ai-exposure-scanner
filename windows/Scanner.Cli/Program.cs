using AIExposureScanner.Scanner;
using AIExposureScanner.Scanner.Filesystem;
using AIExposureScanner.Scanner.Models;
using AIExposureScanner.Scanner.Reporting;

var options = CliOptions.Parse(args);
if (options.ShowHelp)
{
    CliOptions.PrintHelp(Console.Out);
    return 0;
}

if (options.Error is not null)
{
    Console.Error.WriteLine(options.Error);
    CliOptions.PrintHelp(Console.Error);
    return 2;
}

try
{
    var result = await new ScanOrchestrator().ScanAsync(new LocalFilesystem(options.HomeDirectory));
    var reportBuilder = new ReportBuilder();

    if (options.Format == OutputFormat.Pdf)
    {
        WriteBytes(options.OutputPath, reportBuilder.Pdf(result));
        return ExitCodeFor(ReportSummary.FromScanResult(result).Status);
    }

    var report = options.Format switch
    {
        OutputFormat.Markdown => reportBuilder.Markdown(result),
        OutputFormat.Html => reportBuilder.Html(result),
        _ => throw new ArgumentOutOfRangeException(nameof(options.Format), options.Format, null)
    };

    if (options.OutputPath is null)
    {
        Console.Write(report);
    }
    else
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(options.OutputPath));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(options.OutputPath, report);
    }

    return ExitCodeFor(ReportSummary.FromScanResult(result).Status);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Scan failed: {ex.Message}");
    return 2;
}

static int ExitCodeFor(OverallStatus status) =>
    status == OverallStatus.Critical ? 1 : 0;

static void WriteBytes(string? outputPath, byte[] bytes)
{
    if (outputPath is null)
    {
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(bytes);
        return;
    }

    var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
    File.WriteAllBytes(outputPath, bytes);
}

internal enum OutputFormat
{
    Markdown,
    Html,
    Pdf
}

internal sealed record CliOptions(
    OutputFormat Format,
    string? OutputPath,
    string? HomeDirectory,
    bool ShowHelp,
    string? Error
)
{
    public static CliOptions Parse(string[] args)
    {
        var format = OutputFormat.Markdown;
        string? outputPath = null;
        string? homeDirectory = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg)
            {
                case "-h":
                case "--help":
                    return new CliOptions(format, outputPath, homeDirectory, true, null);
                case "--format":
                    if (!TryReadValue(args, ref index, out var formatValue))
                    {
                        return Error("Missing value for --format");
                    }
                    if (formatValue is null)
                    {
                        return Error("Missing value for --format");
                    }
                    if (!TryParseFormat(formatValue, out format))
                    {
                        return Error("Invalid format. Use markdown, html, or pdf.");
                    }
                    break;
                case "--output":
                case "-o":
                    if (!TryReadValue(args, ref index, out outputPath))
                    {
                        return Error("Missing value for --output");
                    }
                    break;
                case "--home":
                    if (!TryReadValue(args, ref index, out homeDirectory))
                    {
                        return Error("Missing value for --home");
                    }
                    break;
                default:
                    return Error($"Unknown argument: {arg}");
            }
        }

        return new CliOptions(format, outputPath, homeDirectory, false, null);

        static CliOptions Error(string message) =>
            new(OutputFormat.Markdown, null, null, false, message);
    }

    public static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine(
            """
            AI Exposure Scanner CLI

            Usage:
              dotnet run --project windows/Scanner.Cli/Scanner.Cli.csproj -- [options]

            Options:
              --format markdown|html|pdf
                                      Output format. Default: markdown.
                                      PDF writes a simple local report PDF.
              --output, -o <path>      Write report to a file instead of stdout.
              --home <path>            Override the home directory used by known-path detectors.
              --help, -h               Show help.

            Exit codes:
              0  Scan completed without critical findings.
              1  Scan completed and critical findings were found.
              2  Invalid arguments or scan failure.
            """
        );
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[++index];
        return true;
    }

    private static bool TryParseFormat(string value, out OutputFormat format)
    {
        switch (value.ToLowerInvariant())
        {
            case "markdown":
            case "md":
                format = OutputFormat.Markdown;
                return true;
            case "html":
                format = OutputFormat.Html;
                return true;
            case "pdf":
                format = OutputFormat.Pdf;
                return true;
            default:
                format = OutputFormat.Markdown;
                return false;
        }
    }
}
