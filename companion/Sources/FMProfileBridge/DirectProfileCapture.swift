import AppKit
import ProfileCore

enum DirectProfileCapture {
    static let bridgeDirectory = ProfileStorage.root.deletingLastPathComponent().appendingPathComponent("Bridge", isDirectory: true)

    static func capture() async throws -> URL {
        let fm = FileManager.default
        let heartbeat = bridgeDirectory.appendingPathComponent("ready")
        guard let modified = (try? fm.attributesOfItem(atPath: heartbeat.path))?[.modificationDate] as? Date,
              Date().timeIntervalSince(modified) < 8,
              let pidText = try? String(contentsOf: heartbeat, encoding: .utf8),
              let pid = Int32(pidText.trimmingCharacters(in: .whitespacesAndNewlines)),
              GamePolicy.isFootballManager(NSRunningApplication(processIdentifier: pid)?.bundleIdentifier) else {
            throw BridgeError.unavailable
        }
        let id = UUID().uuidString.replacingOccurrences(of: "-", with: "").lowercased()
        let request = bridgeDirectory.appendingPathComponent("request")
        let response = bridgeDirectory.appendingPathComponent(id + ".response")
        try id.write(to: request, atomically: true, encoding: .utf8)
        defer {
            try? fm.removeItem(at: response)
            if (try? String(contentsOf: request, encoding: .utf8)) == id { try? fm.removeItem(at: request) }
        }
        let deadline = Date().addingTimeInterval(20)
        while Date() < deadline {
            try Task.checkCancellation()
            if let result = try? String(contentsOf: response, encoding: .utf8) {
                let parts = result.split(separator: "\n", maxSplits: 1, omittingEmptySubsequences: false)
                guard parts.count == 2 else { throw BridgeError.failed("The game returned an incomplete response. Try again.") }
                guard parts[0] == "OK" else { throw BridgeError.failed(String(parts[1])) }
                let directory = URL(fileURLWithPath: String(parts[1]), isDirectory: true).standardizedFileURL
                guard directory.deletingLastPathComponent() == ProfileStorage.root.standardizedFileURL,
                      fm.fileExists(atPath: directory.appendingPathComponent("profile.txt").path) else {
                    throw BridgeError.failed("The game's text export could not be found.")
                }
                try DirectProfileText.prepare(directory: directory)
                try ProfileHandoff.prompt(directory: directory).write(to: directory.appendingPathComponent("prompt.txt"), atomically: true, encoding: .utf8)
                return directory
            }
            try await Task.sleep(nanoseconds: 100_000_000)
        }
        throw BridgeError.failed("FM27 didn't respond within 20 seconds. Keep the game open on a player profile and try again.")
    }

    private enum BridgeError: LocalizedError {
        case unavailable
        case failed(String)
        var errorDescription: String? {
            switch self {
            case .unavailable: return "The direct-export plugin isn't running. Save and quit FM27, then choose FM → Launch FM27 with text export. Load your save and open a player's Overview or Attributes tab. If you configured the BepInEx Steam launch option, launching through Steam also works."
            case .failed(let message): return message
            }
        }
    }
}
