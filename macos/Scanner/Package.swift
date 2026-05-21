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
            dependencies: ["Scanner"],
            resources: [.process("buymeacoffee.png")]
        )
    ]
)
