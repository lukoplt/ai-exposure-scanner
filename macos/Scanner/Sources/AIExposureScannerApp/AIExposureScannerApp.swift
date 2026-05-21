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
    let rulePackStore = RulePackStore()

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

    var detectedTools: [String] {
        guard let facts = result?.facts else { return [] }
        let ids = facts.mcpServers.map(\.appId)
            + facts.settings.map(\.appId)
            + facts.authFiles.map(\.appId)
            + facts.extensions.map(\.appId)
            + facts.configFiles.map(\.appId)
            + facts.appInstallations.filter(\.installed).map(\.appId)
        return Array(Set(ids)).sorted()
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
            let scanResult = try orchestrator.scan(fs: LocalFilesystem(), packs: rulePackStore.activePacks)
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

// MARK: - Rule Pack Store

struct RulePackEntry: Identifiable, Codable {
    var id = UUID()
    var name: String
    var yaml: String
    var isEnabled: Bool = true
    var isValid: Bool = true
    var validationError: String?
}

@MainActor
final class RulePackStore: ObservableObject {
    @Published var entries: [RulePackEntry] = []

    private let udKey = "aiexposure.rulepacks.v1"

    init() { load() }

    func add(yaml: String) {
        switch RulePackLoader.load(yaml: yaml) {
        case .valid(let pack):
            entries.append(RulePackEntry(name: pack.name, yaml: yaml, isEnabled: true, isValid: true))
        case .invalid(let msg):
            entries.append(RulePackEntry(name: "Invalid pack", yaml: yaml, isEnabled: false, isValid: false, validationError: msg))
        }
        save()
    }

    func remove(at offsets: IndexSet) {
        entries.remove(atOffsets: offsets)
        save()
    }

    func toggle(_ entry: RulePackEntry) {
        guard let i = entries.firstIndex(where: { $0.id == entry.id }), entries[i].isValid else { return }
        entries[i].isEnabled.toggle()
        save()
    }

    var activePacks: [RulePack] {
        entries
            .filter { $0.isEnabled && $0.isValid }
            .compactMap {
                if case .valid(let pack) = RulePackLoader.load(yaml: $0.yaml) { return pack }
                return nil
            }
    }

    private func save() {
        if let data = try? JSONEncoder().encode(entries) {
            UserDefaults.standard.set(data, forKey: udKey)
        }
    }

    private func load() {
        guard let data = UserDefaults.standard.data(forKey: udKey),
              let decoded = try? JSONDecoder().decode([RulePackEntry].self, from: data) else { return }
        entries = decoded
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
            HStack(alignment: .center) {
                Image(systemName: "shield.lefthalf.filled")
                    .font(.system(size: 28))
                VStack(alignment: .leading, spacing: 2) {
                    Text(viewModel.text.string(.localAudit))
                        .font(.headline)
                    Text(viewModel.text.string(.privacySubtitle))
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
                Spacer()
                Button {
                    viewModel.scan()
                } label: {
                    Label(viewModel.text.string(.scan), systemImage: "play.fill")
                        .frame(minWidth: 80)
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.regular)
                .disabled(viewModel.isScanning)
            }

            if viewModel.isScanning {
                HStack(spacing: 8) {
                    ProgressView().controlSize(.small)
                    Text(viewModel.text.string(.scan) + "…")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                }
            } else if let summary = viewModel.summary {
                HStack(spacing: 8) {
                    Metric(title: viewModel.text.string(.status), value: summary.status.title(language: viewModel.selectedLanguage))
                    ToolsMetricView(
                        title: viewModel.text.string(.tools),
                        count: summary.toolsFound,
                        tools: viewModel.detectedTools
                    )
                    Metric(title: viewModel.text.string(.mcp), value: "\(summary.mcpServersFound)")
                }
                HStack(spacing: 8) {
                    Metric(title: viewModel.text.string(.critical), value: "\(summary.critical)")
                    Metric(title: viewModel.text.string(.high), value: "\(summary.high)")
                    Metric(title: viewModel.text.string(.medium), value: "\(summary.medium)")
                    Metric(title: viewModel.text.string(.low), value: "\(summary.low)")
                }
            }
        }
    }
}

struct ToolsMetricView: View {
    let title: String
    let count: Int
    let tools: [String]
    @State private var showPopover = false

    var body: some View {
        Button {
            guard !tools.isEmpty else { return }
            showPopover.toggle()
        } label: {
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                HStack(spacing: 4) {
                    Text("\(count)")
                        .font(.headline)
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                    if !tools.isEmpty {
                        Image(systemName: "chevron.down")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            }
            .padding(8)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(.quaternary, in: RoundedRectangle(cornerRadius: 8))
        }
        .buttonStyle(.plain)
        .popover(isPresented: $showPopover, arrowEdge: .bottom) {
            VStack(alignment: .leading, spacing: 8) {
                ForEach(tools, id: \.self) { tool in
                    HStack(spacing: 8) {
                        Image(systemName: "checkmark.circle.fill")
                            .foregroundStyle(.green)
                            .font(.callout)
                        Text(tool)
                            .font(.callout)
                    }
                }
            }
            .padding(14)
            .presentationCompactAdaptation(.popover)
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
        .frame(maxWidth: .infinity, alignment: .leading)
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

            Divider()

            RulePacksSettingsSection(store: viewModel.rulePackStore, text: viewModel.text)

            Divider()

            LabeledContent(viewModel.text.string(.version), value: "0.2.0")

            Link(
                viewModel.text.string(.githubReleases),
                destination: URL(string: "https://github.com/tsechis/ai-exposure-scanner/releases")!
            )

            HStack(spacing: 8) {
                Text("Made with ❤️ by Lukáš Oplt")
                    .font(.footnote)
                    .foregroundStyle(.secondary)
                Link(destination: URL(string: "https://www.buymeacoffee.com/lukasoplt")!) {
                    Image("buymeacoffee", bundle: .module)
                        .resizable()
                        .scaledToFit()
                        .frame(height: 36)
                }
                .buttonStyle(.plain)
            }
        }
        .padding(24)
        .frame(width: 480)
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

// MARK: - Rule Packs Settings

struct RulePacksSettingsSection: View {
    @ObservedObject var store: RulePackStore
    let text: AppText
    @State private var isShowingAddSheet = false

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Text(text.string(.rulePacks))
                    .font(.headline)
                Spacer()
                Button {
                    isShowingAddSheet = true
                } label: {
                    Label(text.string(.addRulePack), systemImage: "plus")
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.small)
            }

            if store.entries.isEmpty {
                Text(text.string(.noRulePacks))
                    .foregroundStyle(.secondary)
                    .font(.caption)
            } else {
                ForEach(store.entries) { entry in
                    HStack {
                        Toggle("", isOn: Binding(
                            get: { entry.isEnabled },
                            set: { _ in store.toggle(entry) }
                        ))
                        .labelsHidden()
                        .disabled(!entry.isValid)

                        VStack(alignment: .leading, spacing: 2) {
                            Text(entry.name)
                                .font(.body)
                            if !entry.isValid, let err = entry.validationError {
                                Text("\(text.string(.invalidPack)): \(err)")
                                    .font(.caption)
                                    .foregroundStyle(.red)
                                    .lineLimit(2)
                            }
                        }
                        Spacer()
                        Button(role: .destructive) {
                            if let i = store.entries.firstIndex(where: { $0.id == entry.id }) {
                                store.remove(at: IndexSet(integer: i))
                            }
                        } label: {
                            Image(systemName: "trash")
                        }
                        .buttonStyle(.borderless)
                        .foregroundStyle(.red)
                    }
                    .padding(8)
                    .background(.quaternary, in: RoundedRectangle(cornerRadius: 6))
                }
            }
        }
        .sheet(isPresented: $isShowingAddSheet) {
            AddRulePackSheet(text: text) { yaml in
                store.add(yaml: yaml)
            }
        }
    }
}

struct AddRulePackSheet: View {
    let text: AppText
    let onAdd: (String) -> Void
    @State private var yaml = ""
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text(text.string(.addRulePack))
                .font(.title3)
                .fontWeight(.semibold)
            Text(text.string(.pasteYaml))
                .foregroundStyle(.secondary)
                .font(.caption)
            TextEditor(text: $yaml)
                .font(.system(.body, design: .monospaced))
                .frame(minHeight: 200)
                .border(.separator)
            HStack {
                Spacer()
                Button(text.string(.done)) {
                    dismiss()
                }
                Button(text.string(.addRulePack)) {
                    let trimmed = yaml.trimmingCharacters(in: .whitespacesAndNewlines)
                    guard !trimmed.isEmpty else { return }
                    onAdd(trimmed)
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
                .disabled(yaml.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .padding(24)
        .frame(width: 520)
    }
}
