public struct AESMCP002: Rule {
    public let id = "AES-MCP-002"
    public let severity: Severity = .critical

    public init() {}

    public func evaluate(_ facts: ScanFacts) -> [Finding] {
        let shells = ["bash", "sh", "zsh", "fish", "cmd", "cmd.exe", "powershell", "powershell.exe", "pwsh", "pwsh.exe"]

        return facts.mcpServers.compactMap { server in
            guard !server.disabled else {
                return nil
            }

            let command = server.commandName.lowercased()
            let commandIsShell = shells.contains(command)
            let hasExecutionArg = server.args.contains { arg in
                let lower = arg.lowercased()
                return lower == "--allow-run" ||
                    lower == "--exec" ||
                    lower.contains("execute") ||
                    (commandIsShell && lower == "-c")
            }

            guard commandIsShell || hasExecutionArg else {
                return nil
            }
            return finding(app: server.appId, serverName: server.name, affectedPath: server.configPath)
        }
    }
}
