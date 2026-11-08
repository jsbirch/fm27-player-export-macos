import Foundation

/// Formats only the active player's attribute block, never the whole Unity document.
/// Older plugins include cached screens in ui.tsv; their coordinates can overlap exactly.
public enum DirectProfileText {
    public static let heading = "Football Manager 27 — player profile (structured UI text)"

    private struct Fragment {
        let x: Double
        let y: Double
        let width: Double
        let height: Double
        let text: String
        let path: String
        var centerY: Double { y + height / 2 }
        var parent: String { String(path.prefix(upTo: path.lastIndex(of: "/")!)) + "/" }
        var rowPath: String? {
            for component in ["/non-clickable-attributes_row/", "/attributes_row/"] {
                if let range = path.range(of: component) { return String(path[..<range.upperBound]) }
            }
            return nil
        }
    }

    public enum FormatError: LocalizedError {
        case invalidData, unsupportedProfile, ambiguousProfile
        public var errorDescription: String? {
            switch self {
            case .invalidData: return "The UI text data is incomplete or invalid. Capture the player again."
            case .unsupportedProfile: return "Couldn’t isolate an individual player's attributes. Open the player's Overview or Attributes tab with the English attribute panels visible, then capture again."
            case .ambiguousProfile: return "More than one player or attribute panel was found. Open a single player's Overview or Attributes tab and capture again."
            }
        }
    }

    public static func render(tsv: String) throws -> String {
        let lines = tsv.components(separatedBy: .newlines).filter { !$0.isEmpty }
        guard lines.first == "x\ty\tw\th\ttext\telementPath" else { throw FormatError.invalidData }
        var fragments: [Fragment] = []
        for line in lines.dropFirst() {
            let columns = line.split(separator: "\t", maxSplits: 5, omittingEmptySubsequences: false).map(String.init)
            guard columns.count == 6, let x = Double(columns[0]), let y = Double(columns[1]),
                  let w = Double(columns[2]), let h = Double(columns[3]),
                  [x, y, w, h].allSatisfy({ $0.isFinite }), w > 0, h > 0,
                  columns[5].hasPrefix("/") else { throw FormatError.invalidData }
            fragments.append(Fragment(x: x, y: y, width: w, height: h, text: columns[4], path: columns[5]))
        }

        // The viewed-entity label prevents an old cached PlayerReport being accepted
        // when the active screen is the Portal, a staff profile or a comparison.
        let names = fragments.filter { $0.path.contains("/ViewedEntityTypeSwitch/Player/") && $0.path.hasSuffix("/PlayerName") }
        guard !names.isEmpty else { throw FormatError.unsupportedProfile }
        guard names.count == 1 else { throw FormatError.ambiguousProfile }
        let reports = fragments.filter { $0.path.contains("/ReportBody/PlayerReport/") }
        let blockMarker = "/PlayerAttributesBlock/"
        let roots = Set(reports.compactMap { f -> String? in
            guard let range = f.path.range(of: blockMarker) else { return nil }
            return String(f.path[..<range.upperBound])
        })
        guard let root = roots.first else { throw FormatError.unsupportedProfile }
        guard roots.count == 1 else { throw FormatError.ambiguousProfile }
        let block = reports.filter { $0.path.hasPrefix(root) }
        let sections = ["Technical", "Set Pieces", "Mental", "Physical", "Goalkeeping"]
        let headings = block.filter { sections.contains($0.text) && $0.rowPath == nil }
        guard headings.contains(where: { $0.text == "Mental" }), headings.contains(where: { $0.text == "Physical" }),
              headings.contains(where: { $0.text == "Technical" || $0.text == "Goalkeeping" }) else {
            throw FormatError.unsupportedProfile
        }
        guard Set(headings.map(\.text)).count == headings.count else { throw FormatError.ambiguousProfile }

        let labels = block.filter { $0.rowPath != nil && $0.path.hasSuffix("/text") }
        let values = block.filter { $0.rowPath != nil && ($0.path.hasSuffix("/badge-text") || $0.path.hasSuffix("/value-text")) }
        var attributes: [String: [(String, String)]] = [:]
        var usableCount = 0
        var unreadableCount = 0
        for label in labels.sorted(by: { $0.centerY < $1.centerY }) {
            guard let section = headings.filter({ label.path.hasPrefix($0.parent) }).max(by: { $0.parent.count < $1.parent.count })?.text else {
                throw FormatError.unsupportedProfile
            }
            if attributes[section, default: []].contains(where: { $0.0 == label.text }) { throw FormatError.ambiguousProfile }
            func sameRow(_ f: Fragment) -> Bool {
                f.rowPath == label.rowPath && abs(f.centerY - label.centerY) <= max(2, min(f.height, label.height) * 0.35)
            }
            // Repeated UI row names need geometry within that row subtree. Never
            // consider nearby numbers from another panel, column or background layer.
            let nextLabelX = labels.filter { sameRow($0) && $0.x > label.x }.map(\.x).min() ?? .infinity
            let candidates = values.filter { sameRow($0) && $0.x >= label.x + label.width - 1 && $0.x < nextLabelX }
            let value: String
            if candidates.count == 1, validRating(candidates[0].text, label: label.text) {
                value = candidates[0].text
                usableCount += 1
            } else {
                value = "[unreadable or ambiguous]"
                unreadableCount += 1
            }
            attributes[section, default: []].append((label.text, value))
        }
        guard usableCount >= 5 else { throw FormatError.unsupportedProfile }

        var output = [heading, "Player (displayed name): \(names[0].text)"]
        func addField(_ title: String, matching predicate: (Fragment) -> Bool) {
            let found = block.filter(predicate)
            guard !found.isEmpty else { return }
            output.append("\(title): \(found.count == 1 ? found[0].text : "[unreadable or ambiguous]")")
        }
        addField("Positions") { $0.path.hasSuffix("/player-pos-text") }
        addField("Selected position") { $0.path.hasSuffix("/selected-pos-text") }
        addField("Height") { $0.path.hasSuffix("/PersonHeightText") }
        addField("Personality") { $0.path.hasSuffix("/PlayerPersonalityText") }
        addField("Reputation") { $0.path.hasSuffix("/PlayerReputationText") }
        addField("Left foot") { $0.path.contains("/FootednessL/") }
        addField("Right foot") { $0.path.contains("/FootednessR/") }
        for section in sections {
            guard let rows = attributes[section], !rows.isEmpty else { continue }
            output += ["", section + ":"] + rows.map { "\($0.0): \($0.1)" }
        }
        let traits = block.filter { $0.path.contains("/PlayerTraitsList/") && $0.path.hasSuffix("/TraitsText") }.sorted { $0.y < $1.y }
        if !traits.isEmpty { output += ["", "Traits:"] + traits.map { "- " + $0.text } }
        output += ["", "Scope: current player's attribute panel only. Displayed values and masked ranges are preserved. Background screens, statistics tables, graphical ratings and unexported profile details are omitted; do not infer missing information."]
        if unreadableCount > 0 { output.append("\(unreadableCount) attribute value(s) could not be paired safely and are marked above.") }
        return output.joined(separator: "\n") + "\n"
    }

    private static func validRating(_ text: String, label: String) -> Bool {
        if ["-", "—", "–", "?", "??"].contains(text) { return true }
        if label == "Goalkeeper Rating" {
            let parts = text.components(separatedBy: "/").map { $0.trimmingCharacters(in: .whitespaces) }
            return parts.count == 2 && parts[1] == "10" && Int(parts[0]).map { (0...10).contains($0) } == true
        }
        let parts = text.components(separatedBy: CharacterSet(charactersIn: "-–—")).map { $0.trimmingCharacters(in: .whitespaces) }
        guard (1...2).contains(parts.count) else { return false }
        let ratings = parts.compactMap(Int.init)
        return ratings.count == parts.count && ratings.allSatisfy { (1...20).contains($0) } && (ratings.count == 1 || ratings[0] <= ratings[1])
    }

    public static func read(directory: URL) throws -> String {
        let source = directory.appendingPathComponent("ui.tsv")
        if FileManager.default.fileExists(atPath: source.path) {
            return try render(tsv: String(contentsOf: source, encoding: .utf8))
        }
        let text = try String(contentsOf: directory.appendingPathComponent("profile.txt"), encoding: .utf8)
        guard text.hasPrefix(heading + "\n") else { throw FormatError.invalidData }
        return text
    }

    @discardableResult
    public static func prepare(directory: URL) throws -> String {
        let text = try read(directory: directory)
        try text.write(to: directory.appendingPathComponent("profile.txt"), atomically: true, encoding: .utf8)
        return text
    }
}
