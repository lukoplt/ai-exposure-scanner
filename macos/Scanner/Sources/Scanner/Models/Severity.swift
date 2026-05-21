public enum Severity: String, Codable, Sendable {
    case critical
    case high
    case medium
    case low

    var sortRank: Int {
        switch self {
        case .critical:
            0
        case .high:
            1
        case .medium:
            2
        case .low:
            3
        }
    }
}
