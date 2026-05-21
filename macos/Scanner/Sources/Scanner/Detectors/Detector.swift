public protocol Detector: Sendable {
    var id: String { get }
    var displayName: String { get }

    func collectFacts(fs: any FilesystemFacade) throws -> ScanFacts
}
