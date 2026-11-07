import Foundation
import ProfileCore

func expect(_ condition: @autoclosure () -> Bool, _ message: String) {
    guard condition() else { fatalError("FAIL: " + message) }
}

let directory = URL(fileURLWithPath: "/Users/A & B/Players/Ødegaard #10+?=test", isDirectory: true)
let components = URLComponents(url: ProfileHandoff.draftURL(directory: directory), resolvingAgainstBaseURL: false)!
expect(components.scheme == "codex" && components.host == "new", "Use the documented new-chat route")
let query = Dictionary(uniqueKeysWithValues: (components.queryItems ?? []).map { ($0.name, $0.value ?? "") })
expect(query["path"] == directory.path, "Workspace path survives URL encoding")
expect(query["prompt"] == ProfileHandoff.prompt(directory: directory), "Complete prompt survives URL encoding")
expect(query["prompt"]!.contains(directory.appendingPathComponent("profile.png").path), "Screenshot path survives URL encoding")
expect(components.fragment == nil, "Hash in a path must not become a URL fragment")
print("PASS: Deep-link paths with spaces, Unicode, &, #, +, ? and =")

expect(GamePolicy.isFootballManager("com.SportsInteractive.FootballManager27"), "FM27 supported")
expect(GamePolicy.isFootballManager("com.SportsInteractive.FootballManager26"), "FM26 supported")
for identifier in [nil, "com.apple.Safari", "com.openai.codex", "Football Manager 27", "com.SportsInteractive.FootballManager27.helper"] {
    expect(!GamePolicy.isFootballManager(identifier), "Do not capture unrelated applications")
}
print("PASS: Only exact FM26/FM27 bundle identifiers are eligible")

let regions = [TextRegion(text: "Passing 12–15", confidence: 0.71, x: 0.1, y: 0.2, width: 0.3, height: 0.04)]
let profile = ProfileCapture(capturedAt: Date(timeIntervalSince1970: 0), sourceApplication: "Football Manager 27",
    sourceBundleID: "com.SportsInteractive.FootballManager27", windowTitle: "Player",
    imageWidth: 2940, imageHeight: 1656, ocrWarning: nil, textRegions: regions)
let decoded = try JSONDecoder().decode(ProfileCapture.self, from: JSONEncoder().encode(profile))
expect(decoded.textRegions == regions, "Masked ratings, confidence and coordinates survive serialization")
expect(decoded.schemaVersion == 1 && decoded.imageWidth == 2940, "Version and image dimensions survive serialization")
print("PASS: Metadata preserves masked ranges, Unicode, OCR confidence and coordinates")

let textDirectory = FileManager.default.temporaryDirectory.appendingPathComponent("fm-text-check-" + UUID().uuidString)
try FileManager.default.createDirectory(at: textDirectory, withIntermediateDirectories: true)
defer { try? FileManager.default.removeItem(at: textDirectory) }
let directText = "Test Player | Passing 12–15 | Tackling 13 | Strength —\n"
try directText.write(to: textDirectory.appendingPathComponent("profile.txt"), atomically: true, encoding: .utf8)
let textPrompt = ProfileHandoff.prompt(directory: textDirectory)
expect(textPrompt.contains(directText.trimmingCharacters(in: .whitespacesAndNewlines)), "Inline direct text preserves masked and missing attributes")
expect(!textPrompt.contains("profile.png") && !textPrompt.contains("image viewer"), "Direct export must not ask the model to load an image")
let directQuery = URLComponents(url: ProfileHandoff.draftURL(directory: textDirectory), resolvingAgainstBaseURL: false)!.queryItems!
expect(directQuery.first(where: { $0.name == "prompt" })?.value == textPrompt, "Direct text survives chat-link encoding")
print("PASS: Direct text is embedded in the prompt without an image request")
