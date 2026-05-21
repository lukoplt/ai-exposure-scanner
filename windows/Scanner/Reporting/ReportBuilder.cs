using System.Globalization;
using System.Net;
using System.Text;
using AIExposureScanner.Scanner.Models;

namespace AIExposureScanner.Scanner.Reporting;

public sealed class ReportBuilder
{
    public string Markdown(ScanResult scanResult, DateTimeOffset? scannedAt = null)
    {
        var summary = ReportSummary.FromScanResult(scanResult);
        var timestamp = Iso8601(scannedAt ?? DateTimeOffset.UtcNow);
        var builder = new StringBuilder()
            .AppendLine("# AI Exposure Scanner Report")
            .AppendLine()
            .AppendLine($"- Scanned at: {timestamp}")
            .AppendLine($"- Overall status: {summary.Status.ToJsonValue()}")
            .AppendLine($"- Tools found: {summary.ToolsFound}")
            .AppendLine($"- MCP servers found: {summary.McpServersFound}")
            .AppendLine($"- Findings: {summary.Critical} critical, {summary.High} high, {summary.Medium} medium, {summary.Low} low")
            .AppendLine();

        if (scanResult.Findings.Count == 0)
        {
            return builder.AppendLine("No findings.").ToString();
        }

        builder.AppendLine("## Findings").AppendLine();
        foreach (var finding in scanResult.Findings)
        {
            builder
                .AppendLine($"### [{finding.Severity.ToJsonValue()}] {finding.RuleId} - {finding.Title}")
                .AppendLine()
                .AppendLine($"- App: {finding.App}");

            if (finding.ServerName is not null)
            {
                builder.AppendLine($"- Server: {finding.ServerName}");
            }
            if (finding.ExtensionId is not null)
            {
                builder.AppendLine($"- Extension: {finding.ExtensionId}");
            }
            if (finding.AffectedPath is not null)
            {
                builder.AppendLine($"- Path: `{finding.AffectedPath}`");
            }
            if (finding.MaskedValue is not null)
            {
                builder.AppendLine($"- Masked value: `{finding.MaskedValue}`");
            }

            builder
                .AppendLine()
                .AppendLine(finding.Explanation)
                .AppendLine()
                .AppendLine($"Recommendation: {finding.Recommendation}")
                .AppendLine();
        }

        return builder.ToString();
    }

    public string Html(ScanResult scanResult, DateTimeOffset? scannedAt = null)
    {
        var summary = ReportSummary.FromScanResult(scanResult);
        var timestamp = Iso8601(scannedAt ?? DateTimeOffset.UtcNow);
        var findingsHtml = scanResult.Findings.Count == 0
            ? """<p class="empty">No findings.</p>"""
            : string.Join(
                Environment.NewLine,
                scanResult.Findings.Select(finding =>
                    $$"""
                    <section class="finding severity-{{EscapeAttribute(finding.Severity.ToJsonValue())}}">
                      <h2><span>{{Escape(finding.Severity.ToJsonValue().ToUpperInvariant())}}</span> {{Escape(finding.RuleId)}} - {{Escape(finding.Title)}}</h2>
                      <dl>
                        <dt>App</dt><dd>{{Escape(finding.App)}}</dd>
                        {{Definition("Server", finding.ServerName)}}
                        {{Definition("Extension", finding.ExtensionId)}}
                        {{Definition("Path", finding.AffectedPath)}}
                        {{Definition("Masked value", finding.MaskedValue)}}
                      </dl>
                      <p>{{Escape(finding.Explanation)}}</p>
                      <p><strong>Recommendation:</strong> {{Escape(finding.Recommendation)}}</p>
                    </section>
                    """
                )
            );

        return $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>AI Exposure Scanner Report</title>
          <style>
            :root { color-scheme: light dark; --bg: #f7f7f5; --fg: #1b1f23; --muted: #5c6670; --line: #d8dee4; --card: #ffffff; }
            @media (prefers-color-scheme: dark) { :root { --bg: #111315; --fg: #f0f3f6; --muted: #a8b0b8; --line: #30363d; --card: #191c20; } }
            body { margin: 0; padding: 32px; background: var(--bg); color: var(--fg); font: 14px/1.5 -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
            main { max-width: 960px; margin: 0 auto; }
            h1 { margin: 0 0 8px; font-size: 28px; }
            .meta { color: var(--muted); margin: 0 0 24px; }
            .summary { display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 12px; margin: 24px 0; }
            .metric, .finding { background: var(--card); border: 1px solid var(--line); border-radius: 8px; padding: 16px; }
            .metric b { display: block; font-size: 22px; }
            .metric span, dt { color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: .04em; }
            .finding { margin: 16px 0; }
            .finding h2 { font-size: 18px; margin: 0 0 12px; }
            .finding h2 span { font-size: 12px; padding: 3px 7px; border-radius: 999px; background: var(--line); }
            dl { display: grid; grid-template-columns: max-content 1fr; gap: 4px 12px; }
            dd { margin: 0; overflow-wrap: anywhere; }
            code { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }
            @media print { body { padding: 0; } .finding, .metric { break-inside: avoid; } }
          </style>
        </head>
        <body>
          <main>
            <h1>AI Exposure Scanner Report</h1>
            <p class="meta">Scanned at {{Escape(timestamp)}}. Overall status: {{Escape(summary.Status.ToJsonValue())}}.</p>
            <section class="summary" aria-label="Summary">
              <div class="metric"><span>Tools</span><b>{{summary.ToolsFound}}</b></div>
              <div class="metric"><span>MCP servers</span><b>{{summary.McpServersFound}}</b></div>
              <div class="metric"><span>Critical</span><b>{{summary.Critical}}</b></div>
              <div class="metric"><span>High</span><b>{{summary.High}}</b></div>
              <div class="metric"><span>Medium</span><b>{{summary.Medium}}</b></div>
              <div class="metric"><span>Low</span><b>{{summary.Low}}</b></div>
            </section>
            {{findingsHtml}}
          </main>
        </body>
        </html>
        """;
    }

    public byte[] Pdf(ScanResult scanResult, DateTimeOffset? scannedAt = null)
    {
        const int maxLineLength = 92;
        const int linesPerPage = 48;

        var lines = PlainTextLines(scanResult, scannedAt ?? DateTimeOffset.UtcNow)
            .SelectMany(line => WrapPdfLine(line, maxLineLength))
            .ToArray();
        if (lines.Length == 0)
        {
            lines = ["AI Exposure Scanner Report", "No findings."];
        }

        var pages = lines.Chunk(linesPerPage).ToArray();
        var objectCount = 3 + pages.Length * 2;
        var offsets = new int[objectCount + 1];
        var output = new StringBuilder("%PDF-1.4\n");
        var byteOffset = Encoding.ASCII.GetByteCount(output.ToString());

        void AppendObject(int number, string body)
        {
            offsets[number] = byteOffset;
            var text = $"{number} 0 obj\n{body}\nendobj\n";
            output.Append(text);
            byteOffset += Encoding.ASCII.GetByteCount(text);
        }

        var kids = string.Join(" ", Enumerable.Range(0, pages.Length).Select(index => $"{4 + index * 2} 0 R"));
        AppendObject(1, "<< /Type /Catalog /Pages 2 0 R >>");
        AppendObject(2, $"<< /Type /Pages /Kids [{kids}] /Count {pages.Length} >>");
        AppendObject(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        for (var index = 0; index < pages.Length; index++)
        {
            var pageObject = 4 + index * 2;
            var contentObject = pageObject + 1;
            var stream = PdfPageContent(pages[index]);
            var streamLength = Encoding.ASCII.GetByteCount(stream);

            AppendObject(
                pageObject,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {contentObject} 0 R >>"
            );
            AppendObject(contentObject, $"<< /Length {streamLength} >>\nstream\n{stream}endstream");
        }

        var xrefOffset = byteOffset;
        output.Append("xref\n");
        output.Append($"0 {objectCount + 1}\n");
        output.Append("0000000000 65535 f \n");
        for (var index = 1; index <= objectCount; index++)
        {
            output.Append($"{offsets[index]:D10} 00000 n \n");
        }
        output.Append($"trailer\n<< /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        return Encoding.ASCII.GetBytes(output.ToString());
    }

    private static string Definition(string label, string? value) =>
        value is null ? "" : $"<dt>{Escape(label)}</dt><dd><code>{Escape(value)}</code></dd>";

    private static string Escape(string value) => WebUtility.HtmlEncode(value);

    private static string EscapeAttribute(string value) => Escape(value);

    private static string Iso8601(DateTimeOffset date) =>
        date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static IEnumerable<string> PlainTextLines(ScanResult scanResult, DateTimeOffset scannedAt)
    {
        var summary = ReportSummary.FromScanResult(scanResult);
        yield return "AI Exposure Scanner Report";
        yield return "";
        yield return $"Scanned at: {Iso8601(scannedAt)}";
        yield return $"Overall status: {summary.Status.ToJsonValue()}";
        yield return $"Tools found: {summary.ToolsFound}";
        yield return $"MCP servers found: {summary.McpServersFound}";
        yield return $"Findings: {summary.Critical} critical, {summary.High} high, {summary.Medium} medium, {summary.Low} low";
        yield return "";

        if (scanResult.Findings.Count == 0)
        {
            yield return "No findings.";
            yield break;
        }

        foreach (var finding in scanResult.Findings)
        {
            yield return $"[{finding.Severity.ToJsonValue()}] {finding.RuleId} - {finding.Title}";
            yield return $"App: {finding.App}";
            if (finding.ServerName is not null)
            {
                yield return $"Server: {finding.ServerName}";
            }
            if (finding.ExtensionId is not null)
            {
                yield return $"Extension: {finding.ExtensionId}";
            }
            if (finding.AffectedPath is not null)
            {
                yield return $"Path: {finding.AffectedPath}";
            }
            if (finding.MaskedValue is not null)
            {
                yield return $"Masked value: {finding.MaskedValue}";
            }
            yield return $"Why this matters: {finding.Explanation}";
            yield return $"Recommended fix: {finding.Recommendation}";
            yield return "";
        }
    }

    private static IEnumerable<string> WrapPdfLine(string line, int maxLength)
    {
        if (line.Length <= maxLength)
        {
            yield return line;
            yield break;
        }

        var remaining = line;
        while (remaining.Length > maxLength)
        {
            var split = remaining.LastIndexOf(' ', maxLength);
            if (split <= 0)
            {
                split = maxLength;
            }

            yield return remaining[..split];
            remaining = remaining[split..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }

    private static string PdfPageContent(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        var y = 742;
        var firstLine = true;

        foreach (var line in lines)
        {
            var fontSize = firstLine ? 16 : 10;
            builder
                .Append("BT /F1 ")
                .Append(fontSize.ToString(CultureInfo.InvariantCulture))
                .Append(" Tf 50 ")
                .Append(y.ToString(CultureInfo.InvariantCulture))
                .Append(" Td (")
                .Append(EscapePdfText(line))
                .Append(") Tj ET\n");
            y -= firstLine ? 24 : 14;
            firstLine = false;
        }

        return builder.ToString();
    }

    private static string EscapePdfText(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\':
                case '(':
                case ')':
                    builder.Append('\\').Append(character);
                    break;
                case < ' ' or > '~':
                    builder.Append('?');
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }
        return builder.ToString();
    }
}
