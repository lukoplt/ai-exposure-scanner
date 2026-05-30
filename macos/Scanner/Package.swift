// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "Scanner",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .library(
            name: "Scanner",
            targets: ["Scanner"]
        ),
        .executable(
            name: "ScannerFixtureTests",
            targets: ["ScannerFixtureTests"]
        ),
        .executable(
            name: "AIExposureScannerApp",
            targets: ["AIExposureScannerApp"]
        ),
        .executable(
            name: "AIExposureUpdater",
            targets: ["AIExposureUpdater"]
        )
    ],
    dependencies: [
        .package(url: "https://github.com/jpsim/Yams.git", from: "5.0.0")
    ],
    targets: [
        .target(
            name: "Scanner",
            dependencies: [.product(name: "Yams", package: "Yams")]
        ),
        .executableTarget(
            name: "ScannerFixtureTests",
            dependencies: ["Scanner"],
            path: "Tests/ScannerFixtureTests"
        ),
        .executableTarget(
            name: "AIExposureScannerApp",
            dependencies: ["Scanner"]
        ),
        // Standalone helper that performs the GitHub Releases update check.
        // Lives in its own target so the Scanner library and the main app
        // can stay free of any network APIs. The main app spawns this
        // binary only when the user has opted into update checks.
        .executableTarget(
            name: "AIExposureUpdater",
            path: "Sources/AIExposureUpdater"
        )
    ]
)
