import AppKit
import Carbon
import ProfileCore

@main
struct FMProfileBridgeMain {
    @MainActor static func main() {
        if CommandLine.arguments.contains("--check-pipeline") {
            do { try PipelineChecks.run() }
            catch { fatalError("Capture pipeline check failed: \(error)") }
            return
        }
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        app.setActivationPolicy(.accessory)
        withExtendedLifetime(delegate) { app.run() }
    }
}

@MainActor
final class AppDelegate: NSObject, NSApplicationDelegate {
    private var statusItem: NSStatusItem!
    private var statusLine: NSMenuItem!
    private var captureItem: NSMenuItem!
    private var hotKey: EventHotKeyRef?
    private var eventHandler: EventHandlerRef?
    private var busy = false
    private let defaults = UserDefaults.standard
    private var latest: URL? {
        get { defaults.string(forKey: "latestCapture").map { URL(fileURLWithPath: $0, isDirectory: true) } }
        set { defaults.set(newValue?.path, forKey: "latestCapture") }
    }

    func applicationDidFinishLaunching(_ notification: Notification) {
        // Avoid two menu bar copies competing for the shortcut.
        if let identifier = Bundle.main.bundleIdentifier,
           NSRunningApplication.runningApplications(withBundleIdentifier: identifier).count > 1 {
            NSApplication.shared.terminate(nil)
            return
        }
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        statusItem.button?.title = "FM"
        statusItem.button?.toolTip = "FM Profile Bridge — capture a player with ⌃⌥⌘P"
        let menu = NSMenu()
        statusLine = menu.addItem(withTitle: "Ready · ⌃⌥⌘P", action: nil, keyEquivalent: "")
        menu.addItem(.separator())
        captureItem = add("Capture player profile", action: #selector(captureFromMenu), to: menu)
        captureItem.keyEquivalent = "p"
        captureItem.keyEquivalentModifierMask = [.control, .option, .command]
        add("Open latest profile in a new chat", action: #selector(openLatestChat), to: menu)
        add("Copy latest prompt for this chat", action: #selector(copyPrompt), to: menu)
        add("Copy latest screenshot", action: #selector(copyScreenshot), to: menu)
        add("Show latest capture", action: #selector(showLatest), to: menu)
        menu.addItem(.separator())
        add("Show all captures", action: #selector(showCaptures), to: menu)
        add("Help & screen recording permission…", action: #selector(showHelp), to: menu)
        menu.addItem(.separator())
        add("Quit FM Profile Bridge", action: #selector(quit), to: menu)
        statusItem.menu = menu
        registerShortcut()
        if !defaults.bool(forKey: "hasShownWelcome") {
            defaults.set(true, forKey: "hasShownWelcome")
            showHelp()
        }
    }

    @discardableResult
    private func add(_ title: String, action: Selector, to menu: NSMenu) -> NSMenuItem {
        let item = menu.addItem(withTitle: title, action: action, keyEquivalent: "")
        item.target = self
        return item
    }

    private func registerShortcut() {
        var event = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let context = Unmanaged.passUnretained(self).toOpaque()
        let installed = InstallEventHandler(GetApplicationEventTarget(), { _, _, context in
            guard let context else { return OSStatus(eventNotHandledErr) }
            let delegate = Unmanaged<AppDelegate>.fromOpaque(context).takeUnretainedValue()
            Task { @MainActor in delegate.captureFromShortcut() }
            return noErr
        }, 1, &event, context, &eventHandler)
        let identifier = EventHotKeyID(signature: 0x464D5042, id: 1)
        let registered = RegisterEventHotKey(UInt32(kVK_ANSI_P), UInt32(controlKey | optionKey | cmdKey),
            identifier, GetApplicationEventTarget(), 0, &hotKey)
        if installed != noErr || registered != noErr {
            statusLine.title = "Shortcut unavailable — use Capture in this menu"
            showAlert("Shortcut unavailable", "Another app may be using ⌃⌥⌘P. You can still capture from the FM menu.")
        }
    }

    private func captureFromShortcut() {
        guard GamePolicy.isFootballManager(NSWorkspace.shared.frontmostApplication?.bundleIdentifier) else {
            statusLine.title = "Open a player in FM26 or FM27 first"
            NSSound.beep()
            return
        }
        capture()
    }

    @objc private func captureFromMenu() { capture() }

    private func capture() {
        guard !busy else { return }
        guard CGPreflightScreenCaptureAccess() else {
            showHelp()
            return
        }
        busy = true
        captureItem.isEnabled = false
        statusItem.button?.title = "FM…"
        statusLine.title = "Capturing player…"
        let preferredPID = NSWorkspace.shared.frontmostApplication?.processIdentifier
        Task {
            defer {
                busy = false
                captureItem.isEnabled = true
                statusItem.button?.title = "FM"
            }
            do {
                let capture = try await GameCapture.capture(preferredPID: preferredPID)
                statusLine.title = "Reading visible text…"
                let directory = try await Task.detached(priority: .userInitiated) {
                    try ProfileStorage.save(capture)
                }.value
                latest = directory
                statusLine.title = "Captured · press Enter in the prepared chat"
                openChat(directory)
            } catch {
                statusLine.title = "Capture failed — try again"
                showAlert("Couldn’t capture the player", error.localizedDescription)
            }
        }
    }

    private func latestCapture() -> URL? {
        guard let latest, FileManager.default.fileExists(atPath: latest.appendingPathComponent("profile.png").path) else {
            showAlert("No saved capture", "Open a player in Football Manager and press ⌃⌥⌘P first.")
            return nil
        }
        return latest
    }

    private func openChat(_ directory: URL) {
        guard NSWorkspace.shared.open(ProfileHandoff.draftURL(directory: directory)) else {
            statusLine.title = "Saved · open ChatGPT and copy the latest prompt"
            showAlert("Profile saved", "Install or open the ChatGPT/Codex desktop app, then choose “Open latest profile in a new chat” from the FM menu. You can also copy the latest screenshot and paste it into a chat.")
            return
        }
    }

    @objc private func openLatestChat() {
        if let directory = latestCapture() { openChat(directory) }
    }

    @objc private func copyPrompt() {
        guard let directory = latestCapture() else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(ProfileHandoff.prompt(directory: directory), forType: .string)
        statusLine.title = "Prompt copied · paste into an existing local chat"
    }

    @objc private func copyScreenshot() {
        guard let directory = latestCapture(),
              let image = NSImage(contentsOf: directory.appendingPathComponent("profile.png")) else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.writeObjects([image])
        statusLine.title = "Screenshot copied · paste into any chat"
    }

    @objc private func showLatest() {
        if let directory = latestCapture() {
            NSWorkspace.shared.activateFileViewerSelecting([directory.appendingPathComponent("profile.png")])
        }
    }

    @objc private func showCaptures() {
        do {
            try FileManager.default.createDirectory(at: ProfileStorage.root, withIntermediateDirectories: true)
            NSWorkspace.shared.open(ProfileStorage.root)
        } catch { showAlert("Couldn’t open captures", error.localizedDescription) }
    }

    @objc private func showHelp() {
        NSApplication.shared.activate(ignoringOtherApps: true)
        let allowed = CGPreflightScreenCaptureAccess()
        let alert = NSAlert()
        alert.messageText = "Send a player profile with ⌃⌥⌘P"
        alert.informativeText = "1. Open a player’s Overview or Attributes tab in FM26 or FM27.\n2. Press Control–Option–Command–P.\n3. A new chat opens with the saved screenshot and OCR paths ready. Press Enter to send.\n\nOnly the game window is captured. Text recognition runs on your Mac. The current tab is saved; hidden attributes and other tabs are not included.\n\nTo use an existing chat, choose “Copy latest prompt for this chat” from the FM menu and paste it there.\n\nScreen Recording: \(allowed ? "enabled" : "permission needed")."
        if !allowed { alert.addButton(withTitle: "Enable Screen Recording…") }
        alert.addButton(withTitle: "Done")
        if alert.runModal() == .alertFirstButtonReturn && !allowed {
            if !CGRequestScreenCaptureAccess() {
                NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture")!)
            }
            statusLine.title = "After enabling Screen Recording, quit and reopen FM Bridge"
        }
    }

    private func showAlert(_ title: String, _ message: String) {
        NSApplication.shared.activate(ignoringOtherApps: true)
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.addButton(withTitle: "OK")
        alert.runModal()
    }

    @objc private func quit() { NSApplication.shared.terminate(nil) }

    func applicationWillTerminate(_ notification: Notification) {
        if let hotKey { UnregisterEventHotKey(hotKey) }
        if let eventHandler { RemoveEventHandler(eventHandler) }
    }
}
