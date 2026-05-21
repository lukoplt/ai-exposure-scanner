import Foundation

public protocol FilesystemFacade: Sendable {
    var homeDirectory: String { get }

    func fileExists(_ path: String) -> Bool
    func directoryExists(_ path: String) -> Bool
    func readTextFile(_ path: String, maxBytes: Int) throws -> String
    func listDirectoryNames(_ path: String) throws -> [String]
    func listFilesRecursively(_ path: String, maxDepth: Int) throws -> [String]
    func isWorldReadableFile(_ path: String) -> Bool
}

public enum FilesystemError: Error, Equatable {
    case fileTooLarge(path: String, size: UInt64, limit: Int)
    case invalidUtf8(path: String)
}

public struct LocalFilesystem: FilesystemFacade, @unchecked Sendable {
    public let homeDirectory: String
    private let fileManager: FileManager

    public init(
        homeDirectory: String = FileManager.default.homeDirectoryForCurrentUser.path,
        fileManager: FileManager = .default
    ) {
        self.homeDirectory = homeDirectory
        self.fileManager = fileManager
    }

    public func fileExists(_ path: String) -> Bool {
        var isDirectory: ObjCBool = false
        return fileManager.fileExists(atPath: path, isDirectory: &isDirectory) && !isDirectory.boolValue
    }

    public func directoryExists(_ path: String) -> Bool {
        var isDirectory: ObjCBool = false
        return fileManager.fileExists(atPath: path, isDirectory: &isDirectory) && isDirectory.boolValue
    }

    public func readTextFile(_ path: String, maxBytes: Int = 10 * 1024 * 1024) throws -> String {
        let attributes = try fileManager.attributesOfItem(atPath: path)
        if let size = attributes[.size] as? UInt64, size > UInt64(maxBytes) {
            throw FilesystemError.fileTooLarge(path: path, size: size, limit: maxBytes)
        }

        let data = try Data(contentsOf: URL(fileURLWithPath: path))
        guard let text = String(data: data, encoding: .utf8) else {
            throw FilesystemError.invalidUtf8(path: path)
        }
        return text
    }

    public func listDirectoryNames(_ path: String) throws -> [String] {
        try fileManager.contentsOfDirectory(atPath: path)
    }

    public func listFilesRecursively(_ path: String, maxDepth: Int) throws -> [String] {
        guard directoryExists(path) else {
            return []
        }
        return try listFilesRecursively(path, currentDepth: 0, maxDepth: maxDepth)
    }

    public func isWorldReadableFile(_ path: String) -> Bool {
        guard let attributes = try? fileManager.attributesOfItem(atPath: path),
              let permissions = attributes[.posixPermissions] as? NSNumber
        else {
            return false
        }
        return permissions.intValue & 0o004 != 0
    }

    private func listFilesRecursively(_ path: String, currentDepth: Int, maxDepth: Int) throws -> [String] {
        guard currentDepth <= maxDepth else {
            return []
        }

        var results: [String] = []
        for name in try listDirectoryNames(path) {
            let child = (path as NSString).appendingPathComponent(name)
            // Skip symlinks to avoid infinite loops on circular references
            let attrs = try? fileManager.attributesOfItem(atPath: child)
            if attrs?[.type] as? FileAttributeType == .typeSymbolicLink { continue }
            if fileExists(child) {
                results.append(child)
            } else if directoryExists(child) {
                results.append(contentsOf: try listFilesRecursively(child, currentDepth: currentDepth + 1, maxDepth: maxDepth))
            }
        }
        return results
    }
}
