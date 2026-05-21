import Foundation

public struct ReportBuilder: Sendable {
    public init() {}

    public func markdown(scanResult: ScanResult, scannedAt: Date = Date()) -> String {
        let summary = ReportSummary(scanResult: scanResult)
        var lines: [String] = [
            "# AI Exposure Scanner Report",
            "",
            "- Scanned at: \(Self.iso8601(scannedAt))",
            "- Overall status: \(summary.status.rawValue)",
            "- Tools found: \(summary.toolsFound)",
            "- MCP servers found: \(summary.mcpServersFound)",
            "- Findings: \(summary.critical) critical, \(summary.high) high, \(summary.medium) medium, \(summary.low) low",
            ""
        ]

        if scanResult.findings.isEmpty {
            lines.append("No findings.")
            lines.append("")
            return lines.joined(separator: "\n")
        }

        lines.append("## Findings")
        lines.append("")

        for finding in scanResult.findings {
            lines.append("### [\(finding.severity.rawValue)] \(finding.ruleId) - \(finding.title)")
            lines.append("")
            lines.append("- App: \(finding.app)")
            if let serverName = finding.serverName {
                lines.append("- Server: \(serverName)")
            }
            if let extensionId = finding.extensionId {
                lines.append("- Extension: \(extensionId)")
            }
            if let affectedPath = finding.affectedPath {
                lines.append("- Path: `\(affectedPath)`")
            }
            if let maskedValue = finding.maskedValue {
                lines.append("- Masked value: `\(maskedValue)`")
            }
            lines.append("")
            lines.append(finding.explanation)
            lines.append("")
            lines.append("Recommendation: \(finding.recommendation)")
            lines.append("")
        }

        return lines.joined(separator: "\n")
    }

    public func html(scanResult: ScanResult, scannedAt: Date = Date()) -> String {
        let summary = ReportSummary(scanResult: scanResult)
        let findingsHtml: String

        if scanResult.findings.isEmpty {
            findingsHtml = #"<p class="empty">No findings.</p>"#
        } else {
            findingsHtml = scanResult.findings.map { finding in
                """
                <section class="finding severity-\(escapeAttribute(finding.severity.rawValue))">
                  <h2><span>\(escape(finding.severity.rawValue.uppercased()))</span> \(escape(finding.ruleId)) - \(escape(finding.title))</h2>
                  <dl>
                    <dt>App</dt><dd>\(escape(finding.app))</dd>
                    \(definition("Server", finding.serverName))
                    \(definition("Extension", finding.extensionId))
                    \(definition("Path", finding.affectedPath))
                    \(definition("Masked value", finding.maskedValue))
                  </dl>
                  <p>\(escape(finding.explanation))</p>
                  <p><strong>Recommendation:</strong> \(escape(finding.recommendation))</p>
                </section>
                """
            }.joined(separator: "\n")
        }

        return """
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
            <p class="meta">Scanned at \(escape(Self.iso8601(scannedAt))). Overall status: \(escape(summary.status.rawValue)).</p>
            <section class="summary" aria-label="Summary">
              <div class="metric"><span>Tools</span><b>\(summary.toolsFound)</b></div>
              <div class="metric"><span>MCP servers</span><b>\(summary.mcpServersFound)</b></div>
              <div class="metric"><span>Critical</span><b>\(summary.critical)</b></div>
              <div class="metric"><span>High</span><b>\(summary.high)</b></div>
              <div class="metric"><span>Medium</span><b>\(summary.medium)</b></div>
              <div class="metric"><span>Low</span><b>\(summary.low)</b></div>
            </section>
            \(findingsHtml)
          </main>
        </body>
        </html>
        """
    }

    private func definition(_ label: String, _ value: String?) -> String {
        guard let value else {
            return ""
        }
        return "<dt>\(escape(label))</dt><dd><code>\(escape(value))</code></dd>"
    }

    private func escape(_ value: String) -> String {
        value
            .replacingOccurrences(of: "&", with: "&amp;")
            .replacingOccurrences(of: "<", with: "&lt;")
            .replacingOccurrences(of: ">", with: "&gt;")
            .replacingOccurrences(of: "\"", with: "&quot;")
    }

    private func escapeAttribute(_ value: String) -> String {
        escape(value)
    }

    private static func iso8601(_ date: Date) -> String {
        ISO8601DateFormatter().string(from: date)
    }
}
