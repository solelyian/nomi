// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "NomiCore",
    platforms: [.iOS(.v17)],
    products: [
        .library(name: "NomiCore", targets: ["NomiCore"])
    ],
    targets: [
        .target(name: "NomiCore"),
        .testTarget(name: "NomiCoreTests", dependencies: ["NomiCore"])
    ]
)
