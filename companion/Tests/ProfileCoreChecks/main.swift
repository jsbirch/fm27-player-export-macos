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
let directText = DirectProfileText.heading + "\nTest Player | Passing 12–15 | Tackling 13 | Strength —\n"
try directText.write(to: textDirectory.appendingPathComponent("profile.txt"), atomically: true, encoding: .utf8)
let textPrompt = ProfileHandoff.prompt(directory: textDirectory)
expect(textPrompt.contains(directText.trimmingCharacters(in: .whitespacesAndNewlines)), "Inline direct text preserves masked and missing attributes")
expect(!textPrompt.contains("profile.png") && !textPrompt.contains("image viewer"), "Direct export must not ask the model to load an image")
let directQuery = URLComponents(url: ProfileHandoff.draftURL(directory: textDirectory), resolvingAgainstBaseURL: false)!.queryItems!
expect(directQuery.first(where: { $0.name == "prompt" })?.value == textPrompt, "Direct text survives chat-link encoding")
print("PASS: Direct text is embedded in the prompt without an image request")

// Regression: FM retains the Portal underneath PlayerReport at overlapping positions.
// Use synthetic text and values; no private player capture is checked into the repo.
let report = "/MainUIDocument-container/ReportBody/PlayerReport/BindingRoot/ComponentElement/NonPhoneLayout/_/GridLayoutElementContent/PlayerAttributesBlock/"
let header = "x\ty\tw\th\ttext\telementPath\n"
func fragment(_ x: Double, _ y: Double, _ text: String, _ path: String, width: Double = 80) -> String {
    "\(x)\t\(y)\t\(width)\t16\t\(text)\t\(path)\n"
}
let name = fragment(50, 50, "Test Player", "/MainUIDocument-container/MenuTitle/ViewedEntityTypeSwitch/Player/PlayerName")
var fixture = header + name
for (section, x) in [("Technical", 100.0), ("Mental", 400.0), ("Physical", 700.0)] {
    fixture += fragment(x, 100, section, report + section + "/_")
}
let checks: [(String, Double, Double, String, String)] = [
    ("Technical", 100, 130, "Passing", "12–15"), ("Technical", 100, 160, "Tackling", "13"), ("Technical", 100, 190, "Technique", "14"),
    ("Mental", 400, 130, "Decisions", "11"), ("Mental", 400, 160, "Flair", "17"), ("Mental", 400, 190, "Vision", "14"),
    ("Physical", 700, 130, "Pace", "13"), ("Physical", 700, 160, "Strength", "—"), ("Physical", 700, 190, "Stamina", "12")
]
for (section, x, y, label, value) in checks {
    let row = report + section + "/List/_/View/_/non-clickable-attributes_row/"
    fixture += fragment(x, y, label, row + "_/text")
    fixture += fragment(x + 150, y + 0.5, value, row + "_/_/badge-text", width: 24)
    fixture += fragment(x + 145, y, "99", "/MainUIDocument-container/MenuBody/PortalScreen/LeagueTable/_/value", width: 24)
}
fixture += fragment(5, 130, "BACKGROUND NEWS AND LEAGUE TABLE", "/MainUIDocument-container/MenuBody/PortalScreen/News/text")
fixture += fragment(100, 600, "OTHER STATS TABLE", report.replacingOccurrences(of: "PlayerAttributesBlock/", with: "PlayerSeasonStats/") + "text")
fixture += fragment(100, 620, "Tries Killer Balls Often", report + "PlayerTraitsList/_/TraitsText")
fixture += fragment(100, 650, "Very Strong", report + "PlayerReportFootednessBlock/FootednessR/_/text")
let clean = try DirectProfileText.render(tsv: fixture)
for (_, _, _, label, value) in checks {
    expect(clean.contains("\(label): \(value)\n"), "Attribute \(label) pairs only with its own UI row")
}
expect(!clean.contains("BACKGROUND") && !clean.contains("99") && !clean.contains("OTHER STATS"), "Exclude Portal and unrelated profile tables")
expect(clean.contains("Tries Killer Balls Often") && clean.contains("Right foot: Very Strong"), "Keep named trait and footedness fields")
print("PASS: Three attribute columns remain paired while overlapping background layers are removed")

func expectRejected(_ data: String, _ message: String) {
    do { _ = try DirectProfileText.render(tsv: data); fatalError("FAIL: " + message) }
    catch { }
}
expectRejected(fixture.replacingOccurrences(of: name, with: ""), "Reject cached PlayerReport when no player is active")
expectRejected(fixture + name, "Reject ambiguous player identity")
expectRejected(fixture + fragment(400, 100, "Mental", report + "Mental/_"), "Reject duplicate comparison panels")
expectRejected(fixture + "NaN\t0\t20\t20\t5\t/root/text\n", "Reject malformed geometry")
let passingPath = report + "Technical/List/_/View/_/non-clickable-attributes_row/_/_/badge-text"
let missing = fixture.replacingOccurrences(of: fragment(250, 130.5, "12–15", passingPath, width: 24), with: "")
let missingText = try DirectProfileText.render(tsv: missing)
expect(missingText.contains("Passing: [unreadable or ambiguous]"), "Missing value must not borrow the Portal's number")
let ambiguous = fixture + fragment(260, 130.5, "16", passingPath, width: 24)
let ambiguousText = try DirectProfileText.render(tsv: ambiguous)
expect(ambiguousText.contains("Passing: [unreadable or ambiguous]"), "Multiple possible values must be flagged")
print("PASS: Cached screens and comparisons are rejected; missing and ambiguous values are flagged")

try fixture.write(to: textDirectory.appendingPathComponent("ui.tsv"), atomically: true, encoding: .utf8)
try "UNSAFE OLD MIXED EXPORT".write(to: textDirectory.appendingPathComponent("profile.txt"), atomically: true, encoding: .utf8)
expect(!ProfileHandoff.prompt(directory: textDirectory).contains("UNSAFE"), "Older captures are cleaned when copied or reopened")
_ = try DirectProfileText.prepare(directory: textDirectory)
let savedText = try String(contentsOf: textDirectory.appendingPathComponent("profile.txt"))
expect(savedText == clean, "Saved text matches the validated handoff")
try "bad header".write(to: textDirectory.appendingPathComponent("ui.tsv"), atomically: true, encoding: .utf8)
expect(!ProfileHandoff.prompt(directory: textDirectory).contains("<player_profile>"), "Invalid diagnostics must not fall back to unvalidated text")
print("PASS: Old mixed exports are repaired and invalid data cannot fall back to raw text")
