import Scanner

enum AppLanguage: String, CaseIterable, Identifiable {
    case english = "en"
    case czech = "cs"

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .english:
            "English"
        case .czech:
            "Čeština"
        }
    }
}

struct AppText {
    enum Key {
        case all
        case scan
        case markdown
        case html
        case pdf
        case json
        case settings
        case severity
        case app
        case noFindings
        case runScan
        case resultsAppear
        case localAudit
        case privacySubtitle
        case status
        case tools
        case mcp
        case critical
        case high
        case medium
        case low
        case whyThisMatters
        case recommendedFix
        case openConfigFile
        case server
        case extensionLabel
        case path
        case maskedValue
        case done
        case language
        case version
        case githubReleases
        case couldNotSavePdf
        case couldNotSaveReport
        case rulePacks
        case addRulePack
        case pasteYaml
        case invalidPack
        case noRulePacks
        case detectedTools
    }

    let language: AppLanguage

    func string(_ key: Key) -> String {
        switch language {
        case .english:
            english(key)
        case .czech:
            czech(key)
        }
    }

    func toolScopesDetected(_ count: Int) -> String {
        switch language {
        case .english:
            "\(count) tool scopes detected"
        case .czech:
            "\(count) detekovaných rozsahů nástrojů"
        }
    }

    private func english(_ key: Key) -> String {
        switch key {
        case .all: "All"
        case .scan: "Scan"
        case .markdown: "Markdown"
        case .html: "HTML"
        case .pdf: "PDF"
        case .json: "JSON"
        case .settings: "Settings"
        case .severity: "Severity"
        case .app: "App"
        case .noFindings: "No findings"
        case .runScan: "Run a scan"
        case .resultsAppear: "Results will appear here."
        case .localAudit: "Local AI Tool Audit"
        case .privacySubtitle: "Nothing leaves this computer."
        case .status: "Status"
        case .tools: "Tools"
        case .mcp: "MCP"
        case .critical: "Critical"
        case .high: "High"
        case .medium: "Medium"
        case .low: "Low"
        case .whyThisMatters: "Why this matters"
        case .recommendedFix: "Recommended fix"
        case .openConfigFile: "Open config file"
        case .server: "Server"
        case .extensionLabel: "Extension"
        case .path: "Path"
        case .maskedValue: "Masked value"
        case .done: "Done"
        case .language: "Language"
        case .version: "Version"
        case .githubReleases: "GitHub Releases"
        case .couldNotSavePdf: "Could not save PDF report"
        case .couldNotSaveReport: "Could not save report"
        case .rulePacks: "Rule Packs"
        case .addRulePack: "Add Rule Pack"
        case .pasteYaml: "Paste YAML"
        case .invalidPack: "Invalid"
        case .noRulePacks: "No rule packs configured."
        case .detectedTools: "Detected tools"
        }
    }

    private func czech(_ key: Key) -> String {
        switch key {
        case .all: "Vše"
        case .scan: "Skenovat"
        case .markdown: "Markdown"
        case .html: "HTML"
        case .pdf: "PDF"
        case .json: "JSON"
        case .settings: "Nastavení"
        case .severity: "Závažnost"
        case .app: "Aplikace"
        case .noFindings: "Bez nálezů"
        case .runScan: "Spusťte sken"
        case .resultsAppear: "Výsledky se zobrazí tady."
        case .localAudit: "Lokální audit AI nástrojů"
        case .privacySubtitle: "Z tohoto počítače nic neodchází."
        case .status: "Stav"
        case .tools: "Nástroje"
        case .mcp: "MCP"
        case .critical: "Kritické"
        case .high: "Vysoké"
        case .medium: "Střední"
        case .low: "Nízké"
        case .whyThisMatters: "Proč na tom záleží"
        case .recommendedFix: "Doporučená oprava"
        case .openConfigFile: "Otevřít konfigurační soubor"
        case .server: "Server"
        case .extensionLabel: "Rozšíření"
        case .path: "Cesta"
        case .maskedValue: "Maskovaná hodnota"
        case .done: "Hotovo"
        case .language: "Jazyk"
        case .version: "Verze"
        case .githubReleases: "GitHub vydání"
        case .couldNotSavePdf: "PDF report se nepodařilo uložit"
        case .couldNotSaveReport: "Report se nepodařilo uložit"
        case .rulePacks: "Balíčky pravidel"
        case .addRulePack: "Přidat balíček"
        case .pasteYaml: "Vložit YAML"
        case .invalidPack: "Neplatný"
        case .noRulePacks: "Žádné balíčky pravidel."
        case .detectedTools: "Detekované nástroje"
        }
    }
}

extension SeverityFilter {
    func title(language: AppLanguage) -> String {
        switch (self, language) {
        case (.all, .english): "All"
        case (.critical, .english): "Critical"
        case (.high, .english): "High"
        case (.medium, .english): "Medium"
        case (.low, .english): "Low"
        case (.all, .czech): "Vše"
        case (.critical, .czech): "Kritické"
        case (.high, .czech): "Vysoké"
        case (.medium, .czech): "Střední"
        case (.low, .czech): "Nízké"
        }
    }
}

extension OverallStatus {
    func title(language: AppLanguage) -> String {
        switch (self, language) {
        case (.critical, .english): "Critical"
        case (.high, .english): "High"
        case (.medium, .english): "Medium"
        case (.low, .english): "Low"
        case (.clean, .english): "Clean"
        case (.critical, .czech): "Kritické"
        case (.high, .czech): "Vysoké"
        case (.medium, .czech): "Střední"
        case (.low, .czech): "Nízké"
        case (.clean, .czech): "Čisté"
        }
    }
}
