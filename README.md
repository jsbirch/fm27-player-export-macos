# FM Player Profile Bridge — macOS

Press **Control–Option–Command–P** in Football Manager to capture a player's current profile and prepare a chat with it.

This fork adds a native Mac menu-bar companion to [oeknudsen's FM27 table exporter](https://github.com/oeknudsen/fm27-player-export-macos). The companion captures an individual player screen without requiring BepInEx, Rosetta, an API key or a game restart. The original table-export plugin remains below.

## Install the profile shortcut

Requires **macOS 14 or later**, Football Manager 26 or 27, and the **ChatGPT/Codex desktop app** for the prepared-chat handoff. Build on your Mac with Apple's Command Line Tools (`xcode-select --install`); there are no third-party Swift packages.

```bash
git clone https://github.com/jsbirch/fm27-player-export-macos.git
cd fm27-player-export-macos
bash install_companion.sh
```

The installer builds `~/Applications/FM Profile Bridge.app` and opens it. Look for **FM** in the menu bar. Enable **FM Profile Bridge** in **System Settings → Privacy & Security → Screen & System Audio Recording** (called Screen Recording on older macOS), then quit and reopen the companion if macOS asks. Audio is never captured. Accessibility permission is not required.

## Share a player

1. Open a player's **Overview** or **Attributes** tab and keep Football Manager in front.
2. Press **⌃⌥⌘P** (Control–Option–Command–P).
3. Wait for the capture and local text recognition. A new local chat opens with the screenshot and metadata paths in its prepared prompt. **Press Enter to send.**

The [documented desktop deep link](https://learn.chatgpt.com/docs/app/commands#deep-links) fills the composer; it does **not** automatically send a message. The receiving local chat can read the saved image. The shortcut does not inject keystrokes into chats or overwrite your clipboard.

To share in an **existing local chat**, choose **FM → Copy latest prompt for this chat**, then paste and send it there. For a web or remote chat that cannot read this Mac's files, choose **Copy latest screenshot** and paste the image instead.

Only the **current game window and tab** are captured. Open other tabs and repeat to share contracts, reports or history. Hidden attributes and obscured information are not recovered. OCR is a convenience and can mix columns or misread values; the image is the primary source. No player capture is committed to this repository.

Each capture is stored locally under:

```text
~/Library/Application Support/FM Profile Bridge/Captures/<timestamp>_<id>/
    profile.png   Original game-window screenshot at display resolution
    profile.json  Source, timestamp, dimensions, OCR text/confidence/coordinates
    ocr.txt       Unstructured OCR transcript
    prompt.txt    Ready-to-paste prompt with local file paths
```

Use **FM → Show all captures** to inspect or delete them. Captures remain until you delete them; the companion has no server or background uploader. Text recognition uses Apple's Vision framework on your Mac. Sending the prepared chat shares the referenced profile with your assistant.

## Build and verify

```bash
swift run --package-path companion ProfileCoreChecks
swift run --package-path companion FMProfileBridge --check-pipeline
bash build_companion.sh                  # dist/FM Profile Bridge.app
# Or choose a destination (including paths with spaces):
bash build_companion.sh /path/to/output
```

The checks exercise URL escaping, capture application restrictions, and metadata preservation. The offline pipeline check renders a synthetic player card and runs the actual PNG writer, Vision OCR, JSON writer and atomic capture storage without recording the screen or opening a chat. The companion is built and ad-hoc signed locally, not Developer ID notarized. Binary builds target the architecture of the Mac that builds them. FM27 is the primary target; FM26 is recognized but needs a live smoke test on your setup.

Troubleshooting:

- **No capture:** confirm the FM menu app is running, Screen Recording is enabled, and FM26/FM27 is frontmost. Try **FM → Capture player profile** to distinguish a hotkey conflict from a capture problem.
- **No game window found:** bring FM to the front and unminimize it. The companion deliberately never falls back to capturing your whole desktop.
- **A non-player screen was captured:** open an individual player's Overview/Attributes and capture again. The helper captures the screen you choose; it does not identify or navigate players for you.
- **Chat doesn't open:** install/update the ChatGPT/Codex desktop app or use **Copy latest screenshot**. Saved captures remain available.
- **Updating:** quit the companion via its FM menu, pull changes and rerun `bash install_companion.sh`. macOS may ask you to re-enable recording permission after rebuilding.
- **Uninstalling:** quit, move `~/Applications/FM Profile Bridge.app` to Trash, and optionally remove its captures folder. The game installation is untouched by the companion.

---

# Original FM27 table exporter — macOS (Apple Silicon)

Export **Football Manager 27** player and staff tables to **CSV + HTML**. Load a view, select players, press **F9**.

It only reads the on-screen FM27 UI — it does not read your save, touch game memory, or modify any game files.

**Credits / lineage:** original **FM26 Player Export** by **vinteset** → macOS fork by **DadMych** ([repo](https://github.com/DadMych/fm26-player-export-macos), MIT) → this **FM27 port**. Runs on **sptndc's FM27 BepInEx** loader ([sortitoutsi thread](https://sortitoutsi.net/content/78080/fm27-bepinex-6-il2cpp-for-macos-apple-silicon-latest-macos-no-game-files-modified)).

**Maintained by:** [oeknudsen](https://github.com/oeknudsen)

---

## Requirements

- macOS on **Apple Silicon** (M1–M4), tested on macOS 27
- **Football Manager 27** (Steam)
- **sptndc's FM27 BepInEx** (the mod loader — install this first): https://sortitoutsi.net/content/78080/fm27-bepinex-6-il2cpp-for-macos-apple-silicon-latest-macos-no-game-files-modified
- **Rosetta 2**

## Installation

1. Install **sptndc's FM27 BepInEx** and set its Steam launch option. **Replace the `USERNAME` placeholder** in that launch option with your real macOS username.
2. Install Rosetta 2:
   ```bash
   softwareupdate --install-rosetta --agree-to-license
   ```
3. Launch FM27 once from Steam, reach the main menu, then quit (this creates the `plugins` folder).
4. Copy `FM27PlayerExport.dll` into:
   ```
   .../Football Manager 27/FM27-BepInEx/BepInEx/plugins/FM27PlayerExport/
   ```
5. Launch FM27.

## Usage

1. Open a **Squad** or **Player Search** screen and load a view with the columns you want (the export captures exactly the visible columns).
2. Select rows (**Cmd+A** = all visible).
3. Press **F9** (or Ctrl+P) and don't touch the mouse while it scrolls.
4. Files are saved to:
   ```
   ~/Sports Interactive/Football Manager 27/FM26PlayerExport by vinteset/Exports CSV/
   ```
   CSV is semicolon-delimited, UTF-8. (HTML copies in `Exports HTML/`.)

## Troubleshooting

- **`Failed to start process for this game : OS Error 260`** → install Rosetta (step 2).
- **Game won't launch / no log** → the Steam launch option still has the literal `USERNAME`; replace it with your macOS username.
- **F9 does nothing** → confirm sptndc's BepInEx loaded (a `BepInEx/LogOutput.log` exists) and that you pressed F9 on a Squad or Player Search screen.
- **Rows empty / only one column** → use a view with real attribute columns and keep hands off the mouse during scroll.

## Known limitations

- macOS Apple Silicon only (verified there).
- Star-rating columns (Ability / Potential) are not yet verified on FM27; text and attribute columns are confirmed working.

---

MIT License (see `LICENSE`). Football Manager is a trademark of Sports Interactive / SEGA; this project is not affiliated with or endorsed by them.
