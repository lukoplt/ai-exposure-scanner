import Foundation

enum FactParsers {
    static func parseMcpServersJson(_ text: String, appId: String, configPath: String) throws -> [McpServerFact] {
        let data = Data(text.utf8)
        let object = try JSONSerialization.jsonObject(with: data)
        guard let root = object as? [String: Any],
              let servers = root["mcpServers"] as? [String: Any]
        else {
            return []
        }
        return parseMcpServers(servers, appId: appId, configPath: configPath)
    }

    static func parseMcpServers(_ servers: [String: Any], appId: String, configPath: String) -> [McpServerFact] {
        servers.compactMap { name, value in
            guard let config = value as? [String: Any] else {
                return nil
            }
            return McpServerFact(
                appId: appId,
                name: name,
                command: config["command"] as? String ?? "",
                args: config["args"] as? [String] ?? [],
                env: config["env"] as? [String: String] ?? [:],
                disabled: config["disabled"] as? Bool ?? false,
                description: config["description"] as? String,
                configPath: configPath
            )
        }
    }

    static func parseCursorPrivacyMode(_ text: String, appId: String, configPath: String) throws -> SettingFact {
        let data = Data(text.utf8)
        let object = try JSONSerialization.jsonObject(with: data)
        let root = object as? [String: Any] ?? [:]
        return SettingFact(
            appId: appId,
            key: "cursor.privacyMode",
            boolValue: root["cursor.privacyMode"] as? Bool,
            configPath: configPath
        )
    }

    static func parseGeminiSettings(_ text: String, appId: String, configPath: String) throws -> ScanFacts {
        let data = Data(text.utf8)
        let object = try JSONSerialization.jsonObject(with: data)
        guard let root = object as? [String: Any] else {
            return ScanFacts()
        }

        var facts = ScanFacts()
        if let servers = root["mcpServers"] as? [String: Any] {
            facts.mcpServers.append(contentsOf: parseMcpServers(servers, appId: appId, configPath: configPath))
        }
        if let apiKey = root["apiKey"] as? String {
            facts.settings.append(
                SettingFact(appId: appId, key: "apiKey", stringValue: apiKey, configPath: configPath)
            )
        }
        return facts
    }

    static func parseCodexToml(_ text: String, appId: String, configPath: String) -> [McpServerFact] {
        var servers: [String: MutableMcpServer] = [:]
        var currentServer: String?
        var currentEnvServer: String?

        for rawLine in text.components(separatedBy: .newlines) {
            let line = rawLine.split(separator: "#", maxSplits: 1).first.map(String.init)?
                .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
            guard !line.isEmpty else {
                continue
            }

            if line.hasPrefix("[") && line.hasSuffix("]") {
                let section = String(line.dropFirst().dropLast())
                if section.hasPrefix("mcp_servers.") {
                    let suffix = String(section.dropFirst("mcp_servers.".count))
                    if suffix.hasSuffix(".env") {
                        let name = String(suffix.dropLast(".env".count))
                        currentServer = name
                        currentEnvServer = name
                        servers[name, default: MutableMcpServer()].name = name
                    } else {
                        currentServer = suffix
                        currentEnvServer = nil
                        servers[suffix, default: MutableMcpServer()].name = suffix
                    }
                } else {
                    currentServer = nil
                    currentEnvServer = nil
                }
                continue
            }

            guard let separator = line.firstIndex(of: "=") else {
                continue
            }
            let key = line[..<separator].trimmingCharacters(in: .whitespacesAndNewlines)
            let value = line[line.index(after: separator)...].trimmingCharacters(in: .whitespacesAndNewlines)

            if let envServer = currentEnvServer {
                servers[envServer, default: MutableMcpServer()].env[key] = parseTomlString(value)
            } else if let server = currentServer {
                switch key {
                case "command":
                    servers[server, default: MutableMcpServer()].command = parseTomlString(value)
                case "args":
                    servers[server, default: MutableMcpServer()].args = parseTomlStringArray(value)
                case "disabled":
                    servers[server, default: MutableMcpServer()].disabled = value.lowercased() == "true"
                case "description":
                    servers[server, default: MutableMcpServer()].description = parseTomlString(value)
                default:
                    break
                }
            }
        }

        return servers.values.map { server in
            McpServerFact(
                appId: appId,
                name: server.name,
                command: server.command,
                args: server.args,
                env: server.env,
                disabled: server.disabled,
                description: server.description,
                configPath: configPath
            )
        }
    }

    private static func parseTomlString(_ value: String) -> String {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmed.count >= 2, trimmed.first == "\"", trimmed.last == "\"" {
            return String(trimmed.dropFirst().dropLast())
                .replacingOccurrences(of: "\\\"", with: "\"")
        }
        return trimmed
    }

    private static func parseTomlStringArray(_ value: String) -> [String] {
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.hasPrefix("[") && trimmed.hasSuffix("]") else {
            return []
        }

        let inner = String(trimmed.dropFirst().dropLast())
        var result: [String] = []
        var current = ""
        var inString = false
        var escaped = false

        for character in inner {
            if escaped {
                current.append(character)
                escaped = false
            } else if character == "\\" {
                escaped = true
            } else if character == "\"" {
                if inString {
                    result.append(current)
                    current = ""
                }
                inString.toggle()
            } else if inString {
                current.append(character)
            }
        }

        return result
    }
}

private struct MutableMcpServer {
    var name = ""
    var command = ""
    var args: [String] = []
    var env: [String: String] = [:]
    var disabled = false
    var description: String?
}
