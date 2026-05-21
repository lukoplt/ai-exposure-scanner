import AppKit
import Scanner
import SwiftUI
import WebKit

@main
struct AIExposureScannerApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
                .frame(minWidth: 980, minHeight: 680)
        }
        .commands {
            CommandGroup(replacing: .newItem) {}
        }
    }
}

@MainActor
final class ScanViewModel: ObservableObject {
    @Published private(set) var result: ScanResult?
    @Published private(set) var summary: ReportSummary?
    @Published private(set) var errorMessage: String?
    @Published private(set) var isScanning = false
    @Published var selectedFindingId: Finding.ID?
    @Published var selectedSeverity: SeverityFilter = .all
    @Published var selectedApp: String = "All"
    @Published var isShowingSettings = false
    @Published var selectedLanguage: AppLanguage = .english

    private let orchestrator: ScanOrchestrator
    private let reportBuilder: ReportBuilder
    private let pdfExporter = PdfReportExporter()

    init(
        orchestrator: ScanOrchestrator = ScanOrchestrator(),
        reportBuilder: ReportBuilder = ReportBuilder()
    ) {
        self.orchestrator = orchestrator
        self.reportBuilder = reportBuilder
    }

    var findings: [Finding] {
        result?.findings ?? []
    }

    var filteredFindings: [Finding] {
        findings.filter { finding in
            selectedSeverity.matches(finding.severity) &&
                (selectedApp == "All" || finding.app == selectedApp)
        }
    }

    var availableApps: [String] {
        ["All"] + Array(Set(findings.map(\.app))).sorted()
    }

    var selectedFinding: Finding? {
        filteredFindings.first { $0.id == selectedFindingId } ?? filteredFindings.first
    }

    var text: AppText {
        AppText(language: selectedLanguage)
    }

    func scan() {
        isScanning = true
        errorMessage = nil

        do {
            let scanResult = try orchestrator.scan(fs: LocalFilesystem())
            result = scanResult
            summary = ReportSummary(scanResult: scanResult)
            selectedSeverity = .all
            selectedApp = "All"
            selectedFindingId = scanResult.findings.first?.id
        } catch {
            errorMessage = String(describing: error)
        }

        isScanning = false
    }

    func exportMarkdown() {
        guard let result else {
            return
        }
        export(
            content: reportBuilder.markdown(scanResult: result),
            suggestedFileName: "AIExposureScanner-Report.md"
        )
    }

    func exportHtml() {
        guard let result else {
            return
        }
        export(
            content: reportBuilder.html(scanResult: result),
            suggestedFileName: "AIExposureScanner-Report.html"
        )
    }

    func exportJson() {
        guard let result else {
            return
        }
        export(
            content: reportBuilder.json(scanResult: result),
            suggestedFileName: "AIExposureScanner-Report.json"
        )
    }

    func exportPdf() {
        guard let result else {
            return
        }

        let panel = NSSavePanel()
        panel.nameFieldStringValue = "AIExposureScanner-Report.pdf"
        panel.canCreateDirectories = true
        guard panel.runModal() == .OK, let url = panel.url else {
            return
        }

        Task {
            do {
                let html = reportBuilder.html(scanResult: result)
                let data = try await pdfExporter.pdfData(html: html)
                try data.write(to: url, options: .atomic)
            } catch {
                errorMessage = "\(text.string(.couldNotSavePdf)): \(error)"
            }
        }
    }

    func openConfigForSelectedFinding() {
        guard let path = selectedFinding?.affectedPath else {
            return
        }
        NSWorkspace.shared.open(URL(fileURLWithPath: path))
    }

    private func export(content: String, suggestedFileName: String) {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = suggestedFileName
        panel.canCreateDirectories = true
        if panel.runModal() == .OK, let url = panel.url {
            do {
                try content.write(to: url, atomically: true, encoding: .utf8)
            } catch {
                errorMessage = "\(text.string(.couldNotSaveReport)): \(error)"
            }
        }
    }
}

enum SeverityFilter: String, CaseIterable, Identifiable {
    case all = "All"
    case critical = "Critical"
    case high = "High"
    case medium = "Medium"
    case low = "Low"

    var id: String { rawValue }

    func matches(_ severity: Severity) -> Bool {
        switch self {
        case .all:
            true
        case .critical:
            severity == .critical
        case .high:
            severity == .high
        case .medium:
            severity == .medium
        case .low:
            severity == .low
        }
    }
}

@MainActor
final class PdfReportExporter: NSObject, WKNavigationDelegate {
    private var navigationContinuation: CheckedContinuation<Void, Error>?

    func pdfData(html: String) async throws -> Data {
        let webView = WKWebView(frame: CGRect(x: 0, y: 0, width: 960, height: 1280))
        webView.navigationDelegate = self

        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            navigationContinuation = continuation
            webView.loadHTMLString(html, baseURL: nil)
        }

        let configuration = WKPDFConfiguration()
        return try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Data, Error>) in
            webView.createPDF(configuration: configuration) { result in
                continuation.resume(with: result)
            }
        }
    }

    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        navigationContinuation?.resume()
        navigationContinuation = nil
    }

    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        navigationContinuation?.resume(throwing: error)
        navigationContinuation = nil
    }
}

extension Finding: Identifiable {
    public var id: String {
        [
            ruleId,
            app,
            serverName ?? "",
            extensionId ?? "",
            affectedPath ?? "",
            maskedValue ?? ""
        ].joined(separator: "|")
    }
}

struct ContentView: View {
    @StateObject private var viewModel = ScanViewModel()

    var body: some View {
        NavigationSplitView {
            Sidebar(viewModel: viewModel)
        } detail: {
            DetailPane(viewModel: viewModel)
        }
        .toolbar {
            ToolbarItemGroup {
                Button {
                    viewModel.scan()
                } label: {
                    Label(viewModel.text.string(.scan), systemImage: "magnifyingglass")
                }
                .disabled(viewModel.isScanning)

                Button {
                    viewModel.exportMarkdown()
                } label: {
                    Label(viewModel.text.string(.markdown), systemImage: "doc.plaintext")
                }
                .disabled(viewModel.result == nil)

                Button {
                    viewModel.exportHtml()
                } label: {
                    Label(viewModel.text.string(.html), systemImage: "doc.richtext")
                }
                .disabled(viewModel.result == nil)

                Button {
                    viewModel.exportPdf()
                } label: {
                    Label(viewModel.text.string(.pdf), systemImage: "doc")
                }
                .disabled(viewModel.result == nil)

                Button {
                    viewModel.exportJson()
                } label: {
                    Label(viewModel.text.string(.json), systemImage: "curlybraces")
                }
                .disabled(viewModel.result == nil)

                Button {
                    viewModel.isShowingSettings = true
                } label: {
                    Label(viewModel.text.string(.settings), systemImage: "gearshape")
                }
            }
        }
        .sheet(isPresented: $viewModel.isShowingSettings) {
            SettingsView(viewModel: viewModel)
        }
        .onAppear {
            if viewModel.result == nil {
                viewModel.scan()
            }
        }
    }
}

struct Sidebar: View {
    @ObservedObject var viewModel: ScanViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Header(viewModel: viewModel)

            if let errorMessage = viewModel.errorMessage {
                Text(errorMessage)
                    .font(.callout)
                    .foregroundStyle(.red)
                    .textSelection(.enabled)
            }

            FilterBar(viewModel: viewModel)

            List(selection: $viewModel.selectedFindingId) {
                ForEach(viewModel.filteredFindings) { finding in
                    FindingRow(finding: finding)
                        .tag(finding.id)
                }
            }
            .overlay {
                if viewModel.filteredFindings.isEmpty && viewModel.result != nil {
                    ContentUnavailableView(viewModel.text.string(.noFindings), systemImage: "checkmark.shield")
                }
            }
        }
        .padding()
        .navigationTitle("AI Exposure Scanner")
    }
}

struct FilterBar: View {
    @ObservedObject var viewModel: ScanViewModel

    var body: some View {
        HStack {
            Picker(viewModel.text.string(.severity), selection: $viewModel.selectedSeverity) {
                ForEach(SeverityFilter.allCases) { filter in
                    Text(filter.title(language: viewModel.selectedLanguage)).tag(filter)
                }
            }
            .pickerStyle(.menu)

            Picker(viewModel.text.string(.app), selection: $viewModel.selectedApp) {
                ForEach(viewModel.availableApps, id: \.self) { app in
                    Text(app == "All" ? viewModel.text.string(.all) : app).tag(app)
                }
            }
            .pickerStyle(.menu)
        }
    }
}

struct Header: View {
    @ObservedObject var viewModel: ScanViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                Image(systemName: "shield.lefthalf.filled")
                    .font(.system(size: 28))
                VStack(alignment: .leading) {
                    Text(viewModel.text.string(.localAudit))
                        .font(.headline)
                    Text(viewModel.text.string(.privacySubtitle))
                        .foregroundStyle(.secondary)
                }
            }

            if viewModel.isScanning {
                ProgressView()
            } else if let summary = viewModel.summary {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 92), spacing: 8)], spacing: 8) {
                    Metric(title: viewModel.text.string(.status), value: summary.status.title(language: viewModel.selectedLanguage))
                    Metric(title: viewModel.text.string(.tools), value: "\(summary.toolsFound)")
                    Metric(title: viewModel.text.string(.mcp), value: "\(summary.mcpServersFound)")
                    Metric(title: viewModel.text.string(.critical), value: "\(summary.critical)")
                    Metric(title: viewModel.text.string(.high), value: "\(summary.high)")
                    Metric(title: viewModel.text.string(.medium), value: "\(summary.medium)")
                    Metric(title: viewModel.text.string(.low), value: "\(summary.low)")
                }
                if summary.toolsFound > 0 {
                    Text(viewModel.text.toolScopesDetected(summary.toolsFound))
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            }
        }
    }
}

struct Metric: View {
    let title: String
    let value: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.caption)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.headline)
                .lineLimit(1)
                .minimumScaleFactor(0.7)
        }
        .padding(8)
        .background(.quaternary, in: RoundedRectangle(cornerRadius: 8))
    }
}

struct FindingRow: View {
    let finding: Finding

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                SeverityBadge(severity: finding.severity)
                Text(finding.ruleId)
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }
            Text(finding.title)
                .font(.callout)
                .lineLimit(2)
            Text([finding.app, finding.serverName, finding.extensionId].compactMap { $0 }.joined(separator: " · "))
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)
        }
        .padding(.vertical, 4)
    }
}

struct DetailPane: View {
    @ObservedObject var viewModel: ScanViewModel

    var body: some View {
        if let finding = viewModel.selectedFinding {
            ScrollView {
                VStack(alignment: .leading, spacing: 20) {
                    HStack {
                        SeverityBadge(severity: finding.severity)
                        Text(finding.ruleId)
                            .font(.headline)
                            .foregroundStyle(.secondary)
                    }

                    Text(finding.title)
                        .font(.title2)
                        .fontWeight(.semibold)

                    Grid(alignment: .leading, horizontalSpacing: 16, verticalSpacing: 10) {
                        DetailRow(label: viewModel.text.string(.app), value: finding.app)
                        DetailRow(label: viewModel.text.string(.server), value: finding.serverName)
                        DetailRow(label: viewModel.text.string(.extensionLabel), value: finding.extensionId)
                        DetailRow(label: viewModel.text.string(.path), value: finding.affectedPath)
                        DetailRow(label: viewModel.text.string(.maskedValue), value: finding.maskedValue)
                    }

                    GroupBox(viewModel.text.string(.whyThisMatters)) {
                        Text(finding.explanation)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .textSelection(.enabled)
                    }

                    GroupBox(viewModel.text.string(.recommendedFix)) {
                        Text(finding.recommendation)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .textSelection(.enabled)
                    }

                    if finding.affectedPath != nil {
                        Button {
                            viewModel.openConfigForSelectedFinding()
                        } label: {
                            Label(viewModel.text.string(.openConfigFile), systemImage: "arrow.up.right.square")
                        }
                    }
                }
                .padding(28)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
        } else {
            ContentUnavailableView(
                viewModel.text.string(.runScan),
                systemImage: "shield",
                description: Text(viewModel.text.string(.resultsAppear))
            )
        }
    }
}

struct SettingsView: View {
    @ObservedObject var viewModel: ScanViewModel
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                Text(viewModel.text.string(.settings))
                    .font(.title2)
                    .fontWeight(.semibold)
                Spacer()
                Button(viewModel.text.string(.done)) {
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
            }

            Picker(viewModel.text.string(.language), selection: $viewModel.selectedLanguage) {
                ForEach(AppLanguage.allCases) { language in
                    Text(language.displayName).tag(language)
                }
            }

            LabeledContent(viewModel.text.string(.version), value: "0.1.0")

            Link(
                viewModel.text.string(.githubReleases),
                destination: URL(string: "https://github.com/ai-exposure-scanner/ai-exposure-scanner/releases")!
            )
        }
        .padding(24)
        .frame(width: 420)
    }
}

struct DetailRow: View {
    let label: String
    let value: String?

    var body: some View {
        if let value, !value.isEmpty {
            GridRow {
                Text(label)
                    .foregroundStyle(.secondary)
                Text(value)
                    .textSelection(.enabled)
            }
        }
    }
}

struct SeverityBadge: View {
    let severity: Severity

    var body: some View {
        Text(severity.rawValue.uppercased())
            .font(.caption2)
            .fontWeight(.semibold)
            .padding(.horizontal, 7)
            .padding(.vertical, 3)
            .background(color.opacity(0.16), in: Capsule())
            .foregroundStyle(color)
    }

    private var color: Color {
        switch severity {
        case .critical:
            .red
        case .high:
            .orange
        case .medium:
            .yellow
        case .low:
            .blue
        }
    }
}
