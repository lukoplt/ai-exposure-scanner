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
        case support
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
        case .support: "Support"
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
        case .support: "Podpořit"
        }
    }
}

// MARK: - Localized rule texts

struct LocalizedRuleText {
    let title: String
    let explanation: String
    let recommendation: String
}

extension AppText {
    func ruleText(for id: String) -> LocalizedRuleText {
        switch language {
        case .english: Self.englishRuleTexts[id] ?? LocalizedRuleText(title: id, explanation: "", recommendation: "")
        case .czech:   Self.czechRuleTexts[id]   ?? Self.englishRuleTexts[id] ?? LocalizedRuleText(title: id, explanation: "", recommendation: "")
        }
    }

    private static let englishRuleTexts: [String: LocalizedRuleText] = [
        "AES-MCP-001": LocalizedRuleText(
            title: "MCP server has broad home directory access",
            explanation: "The MCP server can read from a broad home directory scope.",
            recommendation: "Restrict the server to a specific project folder."),
        "AES-MCP-002": LocalizedRuleText(
            title: "MCP server allows shell command execution",
            explanation: "The MCP server can run shell commands on this machine.",
            recommendation: "Remove this server or isolate it in a sandboxed environment."),
        "AES-MCP-003": LocalizedRuleText(
            title: "MCP server has filesystem access to a broad path",
            explanation: "The MCP server can access a broad filesystem location.",
            recommendation: "Restrict access to the narrow project folder required for the workflow."),
        "AES-MCP-004": LocalizedRuleText(
            title: "MCP server has network access",
            explanation: "The MCP server can fetch remote content or send data over the network.",
            recommendation: "Disable network-capable MCP servers unless they are required."),
        "AES-MCP-005": LocalizedRuleText(
            title: "MCP server reads browser data",
            explanation: "The MCP server can automate or inspect browser sessions.",
            recommendation: "Disable browser automation servers when they are not actively needed."),
        "AES-MCP-006": LocalizedRuleText(
            title: "Unused MCP server still configured",
            explanation: "A disabled or orphaned MCP server remains in configuration.",
            recommendation: "Remove stale MCP server entries."),
        "AES-MCP-007": LocalizedRuleText(
            title: "MCP server runs without pinned version",
            explanation: "The MCP server package can change between runs.",
            recommendation: "Pin the MCP server package to an exact version."),
        "AES-AUTH-001": LocalizedRuleText(
            title: "API key in plain text in MCP server env",
            explanation: "A plain text API key is stored in configuration.",
            recommendation: "Move the key to the OS keychain or a supported secrets manager."),
        "AES-AUTH-002": LocalizedRuleText(
            title: "API key in plain text in MCP server args",
            explanation: "A plain text API key is passed on the command line.",
            recommendation: "Never pass API keys as command-line arguments."),
        "AES-AUTH-003": LocalizedRuleText(
            title: "Auth token file present in plain text",
            explanation: "A local auth token file exists in a known AI tool location.",
            recommendation: "Review whether the token is still needed and keep it outside broad MCP scopes."),
        "AES-CFG-001": LocalizedRuleText(
            title: "Cursor Privacy Mode disabled",
            explanation: "Cursor may send workspace code for AI processing.",
            recommendation: "Enable Cursor Privacy Mode."),
        "AES-CFG-002": LocalizedRuleText(
            title: "Overlapping MCP servers across clients",
            explanation: "The same MCP server name is configured in multiple AI clients.",
            recommendation: "Consolidate duplicate MCP server registrations."),
        "AES-CFG-003": LocalizedRuleText(
            title: "MCP server has no description",
            explanation: "The MCP server has no documented purpose or expected access scope.",
            recommendation: "Add a description explaining why this server exists."),
        "AES-CFG-004": LocalizedRuleText(
            title: "AI tool config file is world-readable",
            explanation: "A configuration file can be read by other users or broad principals.",
            recommendation: "Restrict the config file permissions."),
        "AES-EXT-001": LocalizedRuleText(
            title: "AI coding extension can access terminal",
            explanation: "The extension can execute commands through the VS Code terminal.",
            recommendation: "Disable the extension in sensitive workspaces when not in use.")
    ]

    private static let czechRuleTexts: [String: LocalizedRuleText] = [
        "AES-MCP-001": LocalizedRuleText(
            title: "MCP server má přístup k domovskému adresáři",
            explanation: "MCP server může číst data z širokého rozsahu domovského adresáře.",
            recommendation: "Omezte server na konkrétní složku projektu."),
        "AES-MCP-002": LocalizedRuleText(
            title: "MCP server umožňuje spouštění příkazů shellu",
            explanation: "MCP server může spouštět příkazy shellu na tomto počítači.",
            recommendation: "Odeberte server nebo jej izolujte v sandboxovém prostředí."),
        "AES-MCP-003": LocalizedRuleText(
            title: "MCP server má přístup k širokému místu v souborovém systému",
            explanation: "MCP server může přistupovat k širokému místu v souborovém systému.",
            recommendation: "Omezte přístup na konkrétní složku projektu potřebnou pro práci."),
        "AES-MCP-004": LocalizedRuleText(
            title: "MCP server má přístup k síti",
            explanation: "MCP server může načítat vzdálený obsah nebo odesílat data přes síť.",
            recommendation: "Deaktivujte síťové MCP servery, pokud nejsou nezbytné."),
        "AES-MCP-005": LocalizedRuleText(
            title: "MCP server čte data prohlížeče",
            explanation: "MCP server může automatizovat nebo sledovat relace prohlížeče.",
            recommendation: "Deaktivujte servery pro automatizaci prohlížeče, když je aktivně nepotřebujete."),
        "AES-MCP-006": LocalizedRuleText(
            title: "Nepoužívaný MCP server je stále nakonfigurován",
            explanation: "Zakázaný nebo osiřelý MCP server zůstává v konfiguraci.",
            recommendation: "Odeberte zastaralé záznamy MCP serveru."),
        "AES-MCP-007": LocalizedRuleText(
            title: "MCP server běží bez přesně určené verze",
            explanation: "Balíček MCP serveru se může mezi spuštěními změnit.",
            recommendation: "Přesně určete verzi balíčku MCP serveru."),
        "AES-AUTH-001": LocalizedRuleText(
            title: "API klíč jako prostý text v env MCP serveru",
            explanation: "API klíč je uložen jako prostý text v konfiguraci.",
            recommendation: "Přesuňte klíč do OS Keychain nebo správce tajemství."),
        "AES-AUTH-002": LocalizedRuleText(
            title: "API klíč jako prostý text v argumentech MCP serveru",
            explanation: "API klíč je předáván jako argument příkazové řádky.",
            recommendation: "Nikdy nepředávejte API klíče jako argumenty příkazové řádky."),
        "AES-AUTH-003": LocalizedRuleText(
            title: "Soubor s autentizačním tokenem uložen jako prostý text",
            explanation: "Lokální soubor s tokenem existuje na známém místě AI nástroje.",
            recommendation: "Ověřte, zda je token stále potřeba, a uchovávejte jej mimo rozsah MCP serverů."),
        "AES-CFG-001": LocalizedRuleText(
            title: "Cursor Privacy Mode je vypnutý",
            explanation: "Cursor může odesílat kód pracovního prostoru ke zpracování AI.",
            recommendation: "Zapněte Cursor Privacy Mode."),
        "AES-CFG-002": LocalizedRuleText(
            title: "Překrývající se MCP servery napříč klienty",
            explanation: "Stejný název MCP serveru je nakonfigurován ve více AI klientech.",
            recommendation: "Sloučte duplicitní záznamy MCP serverů."),
        "AES-CFG-003": LocalizedRuleText(
            title: "MCP server nemá popis",
            explanation: "MCP server nemá zdokumentovaný účel ani očekávaný rozsah přístupu.",
            recommendation: "Přidejte popis vysvětlující, proč tento server existuje."),
        "AES-CFG-004": LocalizedRuleText(
            title: "Konfigurační soubor AI nástroje je čitelný pro všechny",
            explanation: "Konfigurační soubor může číst ostatní uživatelé nebo širší skupiny.",
            recommendation: "Omezte oprávnění konfiguračního souboru."),
        "AES-EXT-001": LocalizedRuleText(
            title: "AI rozšíření pro kódování má přístup k terminálu",
            explanation: "Rozšíření může spouštět příkazy přes terminál VS Code.",
            recommendation: "Deaktivujte rozšíření v citlivých pracovních prostorech, když jej nepotřebujete.")
    ]
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
