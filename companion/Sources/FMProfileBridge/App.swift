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
    private var launchProcess: Process?
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
        add("Launch FM27 with text export", action: #selector(launchGame), to: menu)
        captureItem = add("Capture player profile", action: #selector(captureFromMenu), to: menu)
        captureItem.keyEquivalent = "p"
        captureItem.keyEquivalentModifierMask = [.control, .option, .command]
        add("Capture screenshot instead…", action: #selector(captureScreenshot), to: menu)
        add("Open latest profile in a new chat", action: #selector(openLatestChat), to: menu)
        add("Copy latest prompt for this chat", action: #selector(copyPrompt), to: menu)
        add("Copy latest screenshot", action: #selector(copyScreenshot), to: menu)
        add("Show latest capture", action: #selector(showLatest), to: menu)
        menu.addItem(.separator())
        add("Show all captures", action: #selector(showCaptures), to: menu)
        add("Help & setup…", action: #selector(showHelp), to: menu)
        menu.addItem(.separator())
        add("Quit FM Profile Bridge", action: #selector(quit), to: menu)
        statusItem.menu = menu
        registerShortcut()
        if !defaults.bool(forKey: "hasShownTextExportWelcome") {
            defaults.set(true, forKey: "hasShownTextExportWelcome")
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
            statusLine.title = "Open a player in FM27 first"
            NSSound.beep()
            return
        }
        capture()
    }

    @objc private func captureFromMenu() { capture() }

    @objc private func launchGame() {
        guard launchProcess?.isRunning != true else {
            showAlert("FM27 is starting or running", "The loader may take a few minutes on its first launch. Wait for the game to open.")
            return
        }
        guard !NSWorkspace.shared.runningApplications.contains(where: { $0.bundleIdentifier == "com.SportsInteractive.FootballManager27" }) else {
            showAlert("FM27 is already running", "Save and quit FM27 first, then choose this menu item to start it with the text-export plugin.")
            return
        }
        let launcher = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/Steam/steamapps/common/Football Manager 27/FM27-BepInEx/run_fm27_bepinex.sh")
        guard FileManager.default.fileExists(atPath: launcher.path) else {
            showAlert("FM27 loader not found", "Install the FM27 BepInEx loader beside fm.app in your Steam game folder. If you use another Steam library, configure that loader's Steam launch option and launch from Steam.")
            return
        }
        guard NSWorkspace.shared.runningApplications.contains(where: { $0.bundleIdentifier == "com.valvesoftware.steam" }) else {
            showAlert("Open Steam first", "Open Steam and sign in, then choose “Launch FM27 with text export” again.")
            return
        }
        do {
            let process = Process()
            process.executableURL = URL(fileURLWithPath: "/bin/bash")
            process.arguments = [launcher.path]
            process.standardOutput = FileHandle.nullDevice
            process.standardError = FileHandle.nullDevice
            process.terminationHandler = { [weak self] process in
                let status = process.terminationStatus
                Task { @MainActor in
                    self?.launchProcess = nil
                    self?.statusLine.title = status == 0 ? "Ready · ⌃⌥⌘P" : "Game exited · check the loader's launch.log"
                    if status != 0 {
                        self?.showAlert("FM27 loader stopped", "The game or loader exited with code \(status). Details are in FM27-BepInEx/launch.log beside the loader. You can still start the unmodified game from Steam.")
                    }
                }
            }
            try process.run()
            launchProcess = process
            statusLine.title = "Starting FM27 with the text-export plugin…"
        } catch { showAlert("Couldn’t start FM27", error.localizedDescription) }
    }

    private func capture() {
        guard !busy else { return }
        busy = true
        captureItem.isEnabled = false
        statusItem.button?.title = "FM…"
        statusLine.title = "Exporting profile text from the game…"
        Task {
            defer {
                busy = false
                captureItem.isEnabled = true
                statusItem.button?.title = "FM"
            }
            do {
                let directory = try await DirectProfileCapture.capture()
                latest = directory
                statusLine.title = "Text exported · press Enter in the prepared chat"
                openChat(directory)
            } catch {
                statusLine.title = "Text export failed — see setup"
                showAlert("Couldn’t export the player", error.localizedDescription)
            }
        }
    }

    @objc private func captureScreenshot() {
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
        guard let latest, FileManager.default.fileExists(atPath: latest.appendingPathComponent("profile.txt").path)
                || FileManager.default.fileExists(atPath: latest.appendingPathComponent("profile.png").path) else {
            showAlert("No saved capture", "Open a player in Football Manager and press ⌃⌥⌘P first.")
            return nil
        }
        return latest
    }

    private func openChat(_ directory: URL) {
        guard NSWorkspace.shared.open(ProfileHandoff.draftURL(directory: directory)) else {
            statusLine.title = "Saved · open ChatGPT and copy the latest prompt"
            showAlert("Profile saved", "Install or open the ChatGPT/Codex desktop app, then choose “Open latest profile in a new chat” from the FM menu. You can also choose “Copy latest prompt for this chat” and paste it into a chat.")
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
        guard let directory = latestCapture() else { return }
        guard let image = NSImage(contentsOf: directory.appendingPathComponent("profile.png")) else {
            showAlert("This is a text export", "The direct export doesn't create a screenshot. Use “Capture screenshot instead…” if you want an image.")
            return
        }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.writeObjects([image])
        statusLine.title = "Screenshot copied · paste into any chat"
    }

    @objc private func showLatest() {
        if let directory = latestCapture() {
            NSWorkspace.shared.activateFileViewerSelecting([directory])
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
        alert.informativeText = "1. Open Steam, then choose FM → Launch FM27 with text export. (Save and quit an already running game first.)\n2. Open a player’s Overview or Attributes tab (English UI).\n3. Press Control–Option–Command–P. A new chat opens with the profile's actual text. Press Enter to send.\n\nThe shortcut reads visible game UI text directly: no screenshot, OCR or Screen Recording permission is needed. Hidden attributes and other tabs are not included.\n\nTo use an existing chat, choose “Copy latest prompt for this chat” and paste it there.\n\nOptional screenshot fallback: Screen Recording \(allowed ? "enabled" : "not enabled")."
        alert.addButton(withTitle: "Done")
        if !allowed { alert.addButton(withTitle: "Enable optional screenshot capture…") }
        if alert.runModal() == .alertSecondButtonReturn && !allowed {
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
