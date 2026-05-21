public struct McpServerFact: Equatable, Sendable {
    public let appId: String
    public let name: String
    public let command: String
    public let args: [String]
    public let env: [String: String]
    public let disabled: Bool
    public let description: String?
    public let configPath: String?

    public init(
        appId: String,
        name: String,
        command: String,
        args: [String] = [],
        env: [String: String] = [:],
        disabled: Bool = false,
        description: String? = nil,
        configPath: String? = nil
    ) {
        self.appId = appId
        self.name = name
        self.command = command
        self.args = args
        self.env = env
        self.disabled = disabled
        self.description = description
        self.configPath = configPath
    }
}

public struct SettingFact: Equatable, Sendable {
    public let appId: String
    public let key: String
    public let boolValue: Bool?
    public let stringValue: String?
    public let configPath: String?

    public init(
        appId: String,
        key: String,
        boolValue: Bool? = nil,
        stringValue: String? = nil,
        configPath: String? = nil
    ) {
        self.appId = appId
        self.key = key
        self.boolValue = boolValue
        self.stringValue = stringValue
        self.configPath = configPath
    }
}

public struct AuthFileFact: Equatable, Sendable {
    public let appId: String
    public let filePath: String

    public init(appId: String, filePath: String) {
        self.appId = appId
        self.filePath = filePath
    }
}

public struct ExtensionFact: Equatable, Sendable {
    public let appId: String
    public let extensionId: String
    public let extensionName: String
    public let hasTerminalAccess: Bool

    public init(
        appId: String,
        extensionId: String,
        extensionName: String,
        hasTerminalAccess: Bool
    ) {
        self.appId = appId
        self.extensionId = extensionId
        self.extensionName = extensionName
        self.hasTerminalAccess = hasTerminalAccess
    }
}

public struct ConfigFileFact: Equatable, Sendable {
    public let appId: String
    public let path: String
    public let worldReadable: Bool

    public init(appId: String, path: String, worldReadable: Bool) {
        self.appId = appId
        self.path = path
        self.worldReadable = worldReadable
    }
}

public struct AppInstallationFact: Equatable, Sendable {
    public let appId: String
    public let installed: Bool
    public let evidencePath: String?

    public init(appId: String, installed: Bool, evidencePath: String? = nil) {
        self.appId = appId
        self.installed = installed
        self.evidencePath = evidencePath
    }
}

public struct ScanFacts: Equatable, Sendable {
    public var mcpServers: [McpServerFact]
    public var settings: [SettingFact]
    public var authFiles: [AuthFileFact]
    public var extensions: [ExtensionFact]
    public var configFiles: [ConfigFileFact]
    public var appInstallations: [AppInstallationFact]

    public init(
        mcpServers: [McpServerFact] = [],
        settings: [SettingFact] = [],
        authFiles: [AuthFileFact] = [],
        extensions: [ExtensionFact] = [],
        configFiles: [ConfigFileFact] = [],
        appInstallations: [AppInstallationFact] = []
    ) {
        self.mcpServers = mcpServers
        self.settings = settings
        self.authFiles = authFiles
        self.extensions = extensions
        self.configFiles = configFiles
        self.appInstallations = appInstallations
    }

    public mutating func append(_ other: ScanFacts) {
        mcpServers.append(contentsOf: other.mcpServers)
        settings.append(contentsOf: other.settings)
        authFiles.append(contentsOf: other.authFiles)
        extensions.append(contentsOf: other.extensions)
        configFiles.append(contentsOf: other.configFiles)
        appInstallations.append(contentsOf: other.appInstallations)
    }
}
