// AIExposureUpdater
//
// Standalone CLI that asks the GitHub Releases API whether a newer
// version of AI Exposure Scanner is available, and writes the result
// to a well-known cache file the main app reads on launch.
//
// This binary is intentionally separate from the main app so that the
// app and the Scanner library remain free of any network APIs.
// Running this binary is opt-in: the main app spawns it only when the
// user enables "Check for updates on launch" in Settings.
//
// Usage:
//   AIExposureUpdater <current-version>
//
// Output:
//   ~/Library/Caches/com.aiexposurescanner.app/update.json
//     present when a newer version exists; absent otherwise
//
// Exit codes:
//   0  check completed (update.json reflects current state)
//   1  network or parse failure (previous update.json left untouched)
//   2  invalid arguments

import Foundation

let repoSlug = "lukoplt/ai-exposure-scanner"
let releasesUrl = URL(string: "https://api.github.com/repos/\(repoSlug)/releases/latest")!

guard let currentVersion = CommandLine.arguments.dropFirst().first, !currentVersion.isEmpty else {
    FileHandle.standardError.write(Data("usage: AIExposureUpdater <current-version>\n".utf8))
    exit(2)
}

// Cache file location: ~/Library/Caches/com.aiexposurescanner.app/update.json
let cacheDir: URL
do {
    cacheDir = try FileManager.default
        .url(for: .cachesDirectory, in: .userDomainMask, appropriateFor: nil, create: true)
        .appendingPathComponent("com.aiexposurescanner.app", isDirectory: true)
    try FileManager.default.createDirectory(at: cacheDir, withIntermediateDirectories: true)
} catch {
    FileHandle.standardError.write(Data("could not create cache dir: \(error.localizedDescription)\n".utf8))
    exit(1)
}
let resultFile = cacheDir.appendingPathComponent("update.json")

// Short timeouts so an unreachable network never blocks the parent app.
let config = URLSessionConfiguration.ephemeral
config.timeoutIntervalForRequest = 10
config.timeoutIntervalForResource = 10
config.requestCachePolicy = .reloadIgnoringLocalAndRemoteCacheData
let session = URLSession(configuration: config)

var request = URLRequest(url: releasesUrl)
request.setValue("AIExposureScanner-Updater/\(currentVersion)", forHTTPHeaderField: "User-Agent")
request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")

let payload: (Data, URLResponse)
do {
    payload = try await session.data(for: request)
} catch {
    FileHandle.standardError.write(Data("update check failed: \(error.localizedDescription)\n".utf8))
    exit(1)
}

let (data, response) = payload
let statusCode = (response as? HTTPURLResponse)?.statusCode ?? -1

// 404 from /releases/latest means the repo has no published releases yet.
// That is a valid "nothing newer" state — clear any stale notification and
// exit successfully so the parent app does not show a confusing error.
if statusCode == 404 {
    try? FileManager.default.removeItem(at: resultFile)
    print("no releases published yet")
    exit(0)
}

guard (200..<300).contains(statusCode) else {
    FileHandle.standardError.write(Data("GitHub API returned HTTP \(statusCode)\n".utf8))
    exit(1)
}

guard
    let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
    let tagName = json["tag_name"] as? String,
    let htmlUrl = json["html_url"] as? String
else {
    FileHandle.standardError.write(Data("GitHub API response did not parse\n".utf8))
    exit(1)
}

// Tags are "v0.3.0"; strip the leading "v" before comparing.
let latestVersion = tagName.hasPrefix("v") ? String(tagName.dropFirst()) : tagName

// `String.compare(_:options: .numeric)` does sane version-number ordering
// for dotted release tags ("0.10.0" > "0.9.0", "0.2.1" > "0.2.0").
let isNewer = latestVersion.compare(currentVersion, options: .numeric) == .orderedDescending

if isNewer {
    let result: [String: Any] = [
        "version": latestVersion,
        "url": htmlUrl,
        "checkedAt": ISO8601DateFormatter().string(from: Date())
    ]
    do {
        let bytes = try JSONSerialization.data(withJSONObject: result, options: [.prettyPrinted, .sortedKeys])
        try bytes.write(to: resultFile, options: .atomic)
    } catch {
        FileHandle.standardError.write(Data("could not write update.json: \(error.localizedDescription)\n".utf8))
        exit(1)
    }
    print("update available: \(latestVersion)")
} else {
    // Already up to date — remove any stale notification from a previous run.
    try? FileManager.default.removeItem(at: resultFile)
    print("up to date (\(currentVersion))")
}
exit(0)
