public enum OverallStatus: String, Codable, Sendable {
    case critical
    case high
    case medium
    case low
    case clean
}

public struct ReportSummary: Equatable, Codable, Sendable {
    public let toolsFound: Int
    public let mcpServersFound: Int
    public let critical: Int
    public let high: Int
    public let medium: Int
    public let low: Int
    public let status: OverallStatus

    public init(
        toolsFound: Int,
        mcpServersFound: Int,
        critical: Int,
        high: Int,
        medium: Int,
        low: Int,
        status: OverallStatus
    ) {
        self.toolsFound = toolsFound
        self.mcpServersFound = mcpServersFound
        self.critical = critical
        self.high = high
        self.medium = medium
        self.low = low
        self.status = status
    }

    public init(scanResult: ScanResult) {
        let findings = scanResult.findings
        let appsWithFacts = Set(
            scanResult.facts.mcpServers.map(\.appId) +
                scanResult.facts.settings.map(\.appId) +
                scanResult.facts.authFiles.map(\.appId) +
                scanResult.facts.extensions.map(\.appId) +
                scanResult.facts.configFiles.map(\.appId) +
                scanResult.facts.appInstallations.filter(\.installed).map(\.appId)
        )

        let critical = findings.filter { $0.severity == .critical }.count
        let high = findings.filter { $0.severity == .high }.count
        let medium = findings.filter { $0.severity == .medium }.count
        let low = findings.filter { $0.severity == .low }.count

        self.init(
            toolsFound: appsWithFacts.count,
            mcpServersFound: scanResult.facts.mcpServers.count,
            critical: critical,
            high: high,
            medium: medium,
            low: low,
            status: ReportSummary.status(critical: critical, high: high, medium: medium, low: low)
        )
    }

    private static func status(critical: Int, high: Int, medium: Int, low: Int) -> OverallStatus {
        if critical > 0 {
            return .critical
        }
        if high > 0 {
            return .high
        }
        if medium > 0 {
            return .medium
        }
        if low > 0 {
            return .low
        }
        return .clean
    }
}
