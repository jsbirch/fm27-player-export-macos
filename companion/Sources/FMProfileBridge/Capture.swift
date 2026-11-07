import AppKit
import ScreenCaptureKit
import Vision
import ImageIO
import UniformTypeIdentifiers
import ProfileCore

enum CaptureError: LocalizedError {
    case noGameWindow
    case saveImage

    var errorDescription: String? {
        switch self {
        case .noGameWindow:
            return "No visible Football Manager 26 or 27 window was found. Open a player’s Overview or Attributes tab, bring the game to the front, and try again."
        case .saveImage:
            return "The screenshot could not be saved. Check the free space on your Mac and try again."
        }
    }
}

struct CapturedWindow {
    let image: CGImage
    let application: String
    let bundleID: String
    let title: String
    let capturedAt: Date
}

enum ProfileStorage {
    static var root: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("FM Profile Bridge/Captures", isDirectory: true)
    }

    static func save(_ capture: CapturedWindow, to root: URL = ProfileStorage.root) throws -> URL {
        let fm = FileManager.default
        try fm.createDirectory(at: root, withIntermediateDirectories: true)
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd_HH-mm-ss"
        let name = formatter.string(from: capture.capturedAt) + "_" + UUID().uuidString.prefix(8)
        let directory = root.appendingPathComponent(name, isDirectory: true)
        let staging = root.appendingPathComponent(".pending-" + UUID().uuidString, isDirectory: true)
        try fm.createDirectory(at: staging, withIntermediateDirectories: false)
        defer { try? fm.removeItem(at: staging) }

        let png = staging.appendingPathComponent("profile.png")
        guard let destination = CGImageDestinationCreateWithURL(png as CFURL, UTType.png.identifier as CFString, 1, nil) else {
            throw CaptureError.saveImage
        }
        CGImageDestinationAddImage(destination, capture.image, nil)
        guard CGImageDestinationFinalize(destination) else { throw CaptureError.saveImage }

        var regions: [TextRegion] = []
        var warning: String?
        do {
            let request = VNRecognizeTextRequest()
            request.recognitionLevel = .accurate
            request.usesLanguageCorrection = false
            request.automaticallyDetectsLanguage = true
            try VNImageRequestHandler(cgImage: capture.image, options: [:]).perform([request])
            regions = (request.results ?? []).compactMap { observation in
                guard let candidate = observation.topCandidates(1).first else { return nil }
                let box = observation.boundingBox
                return TextRegion(text: candidate.string, confidence: candidate.confidence,
                                  x: box.minX, y: 1 - box.maxY, width: box.width, height: box.height)
            }.sorted { $0.y == $1.y ? $0.x < $1.x : $0.y < $1.y }
            if regions.isEmpty { warning = "No text was recognized. Inspect the screenshot directly." }
        } catch {
            // A failed OCR pass must not discard a usable screenshot.
            warning = "Text recognition failed: \(error.localizedDescription). Inspect the screenshot directly."
        }

        let metadata = ProfileCapture(capturedAt: capture.capturedAt,
            sourceApplication: capture.application, sourceBundleID: capture.bundleID,
            windowTitle: capture.title, imageWidth: capture.image.width, imageHeight: capture.image.height,
            ocrWarning: warning, textRegions: regions)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
        encoder.dateEncodingStrategy = .iso8601
        try encoder.encode(metadata).write(to: staging.appendingPathComponent("profile.json"), options: .atomic)
        let ocr = "OCR transcript — may mix columns or misread attributes. Use profile.png as the source.\n\n"
            + regions.map(\.text).joined(separator: "\n") + "\n"
        try ocr.write(to: staging.appendingPathComponent("ocr.txt"), atomically: true, encoding: .utf8)
        try ProfileHandoff.prompt(directory: directory).write(to: staging.appendingPathComponent("prompt.txt"), atomically: true, encoding: .utf8)
        try fm.moveItem(at: staging, to: directory)
        return directory
    }
}

enum GameCapture {
    static func capture(preferredPID: pid_t?) async throws -> CapturedWindow {
        let content = try await SCShareableContent.excludingDesktopWindows(true, onScreenWindowsOnly: true)
        let candidates = content.windows.filter {
            GamePolicy.isFootballManager($0.owningApplication?.bundleIdentifier)
                && $0.windowLayer == 0 && $0.frame.width >= 320 && $0.frame.height >= 240
        }.sorted { a, b in
            let aPreferred = a.owningApplication?.processID == preferredPID
            let bPreferred = b.owningApplication?.processID == preferredPID
            if aPreferred != bPreferred { return aPreferred }
            return a.frame.width * a.frame.height > b.frame.width * b.frame.height
        }
        guard let window = candidates.first, let owner = window.owningApplication else {
            throw CaptureError.noGameWindow
        }
        let filter = SCContentFilter(desktopIndependentWindow: window)
        let config = SCStreamConfiguration()
        config.width = Int((filter.contentRect.width * CGFloat(filter.pointPixelScale)).rounded())
        config.height = Int((filter.contentRect.height * CGFloat(filter.pointPixelScale)).rounded())
        config.showsCursor = false
        config.ignoreShadowsSingleWindow = true
        config.captureResolution = .best
        let image = try await SCScreenshotManager.captureImage(contentFilter: filter, configuration: config)
        return CapturedWindow(image: image, application: owner.applicationName,
                              bundleID: owner.bundleIdentifier, title: window.title ?? "Football Manager",
                              capturedAt: Date())
    }
}
