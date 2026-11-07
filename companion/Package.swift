// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "FMProfileBridge",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "FMProfileBridge", targets: ["FMProfileBridge"])],
    targets: [
        .target(name: "ProfileCore"),
        .executableTarget(name: "FMProfileBridge", dependencies: ["ProfileCore"]),
        // Runs with Command Line Tools alone; Apple's XCTest requires full Xcode.
        .executableTarget(name: "ProfileCoreChecks", dependencies: ["ProfileCore"], path: "Tests/ProfileCoreChecks")
    ]
)
