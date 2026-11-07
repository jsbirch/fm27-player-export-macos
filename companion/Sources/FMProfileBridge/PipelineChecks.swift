// Offline smoke test: exercises the real PNG / Vision OCR / metadata writer.
// No screen recording, app activation, clipboard mutation or network requests.
import AppKit
import CoreText
import ImageIO
import ProfileCore

enum PipelineChecks {
    static func run() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("fm-profile-check-" + UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let context = CGContext(data: nil, width: 1200, height: 700, bitsPerComponent: 8, bytesPerRow: 0,
            space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        context.setFillColor(CGColor(gray: 1, alpha: 1))
        context.fill(CGRect(x: 0, y: 0, width: 1200, height: 700))
        for (index, text) in ["Test Player", "Passing 14", "Pace 16", "Potential 12-15"].enumerated() {
            let string = NSAttributedString(string: text, attributes: [
                .font: NSFont.systemFont(ofSize: 48), .foregroundColor: NSColor.black
            ])
            context.textPosition = CGPoint(x: 60, y: 600 - index * 100)
            CTLineDraw(CTLineCreateWithAttributedString(string), context)
        }
        let date = Date()
        let capture = CapturedWindow(image: context.makeImage()!, application: "Football Manager 27",
            bundleID: "com.SportsInteractive.FootballManager27", title: "Offline test fixture", capturedAt: date)
        let directory = try ProfileStorage.save(capture, to: root)
        let files = Set(try FileManager.default.contentsOfDirectory(atPath: directory.path))
        guard files == Set(["profile.png", "profile.json", "ocr.txt", "prompt.txt"]) else {
            throw CheckError.failed("Incomplete capture bundle")
        }
        let source = CGImageSourceCreateWithURL(directory.appendingPathComponent("profile.png") as CFURL, nil)!
        let image = CGImageSourceCreateImageAtIndex(source, 0, nil)!
        guard image.width == 1200 && image.height == 700 else { throw CheckError.failed("PNG dimensions changed") }
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        let metadata = try decoder.decode(ProfileCapture.self, from: Data(contentsOf: directory.appendingPathComponent("profile.json")))
        let transcript = metadata.textRegions.map(\.text).joined(separator: " ")
        guard transcript.contains("Passing"), transcript.contains("14"), metadata.ocrWarning == nil else {
            throw CheckError.failed("OCR did not recover fixture values: " + transcript)
        }
        guard metadata.textRegions.allSatisfy({ $0.x >= 0 && $0.y >= 0 && $0.x + $0.width <= 1.001 && $0.y + $0.height <= 1.001 }) else {
            throw CheckError.failed("OCR coordinates are outside the image")
        }
        let prompt = try String(contentsOf: directory.appendingPathComponent("prompt.txt"), encoding: .utf8)
        guard prompt == ProfileHandoff.prompt(directory: directory), !prompt.contains(".pending-") else {
            throw CheckError.failed("Prompt points to a temporary location")
        }
        let secondDirectory = try ProfileStorage.save(capture, to: root)
        guard secondDirectory != directory else { throw CheckError.failed("Repeated captures overwrite each other") }
        let children = try FileManager.default.contentsOfDirectory(atPath: root.path)
        guard children.count == 2, !children.contains(where: { $0.hasPrefix(".pending-") }) else {
            throw CheckError.failed("Staging directory leaked")
        }
        print("PASS: Real PNG encoding, Vision OCR, image coordinates, JSON, final paths, unique captures and atomic bundle writes")
    }

    private enum CheckError: Error { case failed(String) }
}
