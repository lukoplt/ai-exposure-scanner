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
        )
    ],
    targets: [
        .target(
            name: "Scanner"
        ),
        .executableTarget(
            name: "ScannerFixtureTests",
            dependencies: ["Scanner"],
            path: "Tests/ScannerFixtureTests"
        ),
        .executableTarget(
            name: "AIExposureScannerApp",
            dependencies: ["Scanner"]
        )
    ]
)
