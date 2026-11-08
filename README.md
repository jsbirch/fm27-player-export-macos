# FM Player Profile Bridge — macOS

Press **Control–Option–Command–P** in FM27 to export a player's visible profile **as text** and prepare a chat with it. No image or OCR is used by the default shortcut.

This fork extends [oeknudsen's FM27 table exporter](https://github.com/oeknudsen/fm27-player-export-macos) with a direct profile-text plugin and native Mac menu-bar companion. The original F9 table export remains available.

## Install

Requires an **Apple Silicon Mac**, **macOS 14 or later**, **Football Manager 27 through Steam**, the game's **English UI**, and the **ChatGPT/Codex desktop app** for the prepared-chat handoff.

1. Install [codexx01's FM27 BepInEx package](https://sortitoutsi.net/content/78080/fm27-bepinex-6-il2cpp-for-macos-apple-silicon-latest-macos-no-game-files-modified), based on sptndc's loader. Put its `FM27-BepInEx` folder beside `fm.app` in your Steam game directory. Follow the package's installation instructions.
2. Install Rosetta 2 if needed: `softwareupdate --install-rosetta --agree-to-license`.
3. Clone this fork and install the plugin and companion:

   ```bash
   git clone https://github.com/jsbirch/fm27-player-export-macos.git
   cd fm27-player-export-macos
   bash install_plugin.sh
   bash install_companion.sh
   ```

The companion builds with Apple's Command Line Tools (`xcode-select --install`). No third-party Swift packages or API key are required. The plugin installer copies the prebuilt DLL; .NET is needed only when rebuilding it.

The app installs to `~/Applications/FM Profile Bridge.app`. Look for **FM** in the menu bar. **Screen Recording and Accessibility permissions are not needed for text export.**

Installation can finish while FM27 is running. The plugin takes effect on the next launch through BepInEx. **Save and quit the game when you are ready**, leave Steam open, then choose **FM → Launch FM27 with text export**. The first launch may take several minutes to prepare the loader's shadow game copy and references.

The menu launcher uses Steam's default library path. For another library, set `FM27_BEP` when installing the plugin and configure the loader's Steam launch option using its actual path:

```text
"/your/game/folder/FM27-BepInEx/run_fm27_bepinex.sh" %command%
```

Without that Steam launch option, starting FM27 normally from Steam runs without the text-export plugin. Use the FM menu launcher for text export. The original `fm.app` is not changed.

## Share a player

1. Load your save, open an individual player's **Overview** or **Attributes** tab and make the attribute panels visible. Keep FM27 in front.
2. Press **⌃⌥⌘P** (Control–Option–Command–P).
3. A new chat opens with the exported text in its prompt. **Press Enter to send.**

The companion uses the desktop app's `codex://new` deep link to prepare the composer. It does not send a message or inject keystrokes. To use an existing chat, choose **FM → Copy latest prompt for this chat**, then paste and send it there. Direct text also works in a web chat.

The plugin reads game UI text on Unity's main thread, excluding hidden/offscreen elements and tooltips. Unity can retain background screens in that UI tree, so the companion verifies the active player identity and isolates `ReportBody/PlayerReport/PlayerAttributesBlock`. It pairs each attribute label with a value from the same UI row and section; background coordinates are never used to match values. Masked ranges are preserved exactly, missing or ambiguous values are flagged, and unsupported layouts fail without sending a mixed transcript. Only the **current player's attribute panel** and named fields such as positions, feet, personality and traits are shared. Statistics tables and graphical ratings are omitted. Hidden ability, potential and unseen tabs are not recovered. English UI is currently required.

Each export is stored locally under:

```text
~/Library/Application Support/FM Profile Bridge/Captures/<timestamp>_<id>/
    profile.txt   Clean player fields and one attribute/value pair per line
    ui.tsv        Raw diagnostic text, bounds and element paths (may include cached screens)
    prompt.txt    Ready-to-paste assessment prompt
```

`ui.tsv` is used locally to isolate the player; its raw contents are not included in the prepared prompt. The 2.0.1 companion also cleans older captures when copying or reopening them. This fix works with plugin 5.2.0, so updating the companion alone does not require restarting FM27. Use **Show all captures** to inspect or delete saved exports. The helper has no server or background uploader. Sending the prepared chat shares the profile text with your assistant.

## Optional screenshot capture

**FM → Capture screenshot instead…** retains the earlier screenshot/OCR workflow for FM26 or FM27. It requires Screen Recording permission and saves `profile.png`, `profile.json`, `ocr.txt` and `prompt.txt`. Only the game window is captured. OCR can misread values and mix columns, so this separate workflow asks the assistant to check the image. A failed direct-text export never silently falls back to an image.

## Build and verify

```bash
swift run --package-path companion ProfileCoreChecks
swift run --package-path companion FMProfileBridge --check-pipeline
bash build_companion.sh                   # dist/FM Profile Bridge.app
bash build_plugin.sh                      # requires .NET SDK and generated BepInEx interop
bash install_plugin.sh
```

`build_plugin.sh` uses the loader in Steam's default library, or `FM27_BEP` if set. BepInEx normally generates the interop references on first launch. The companion checks cover URL encoding, direct-text handoff with masked/missing values, application restrictions, metadata, the optional screenshot/OCR pipeline, and player-panel isolation/attribute pairing regressions.

**Validation status:** plugin 5.2.0 has loaded successfully in a running FM27 game and produced direct UI data. Companion 2.0.1 fixes the background-screen mixing found in the first live player capture. The saved failing capture now yields 37 labelled ratings with the Portal content removed. Regression checks cover overlapping background layers, masked ranges, missing/ambiguous values, cached profiles, duplicate comparison panels and old-capture repair. The new formatter has been tested against that saved live capture; validation on additional live layouts remains outstanding.

## Troubleshooting

- **Plugin isn't running:** save and quit FM27, then use **FM → Launch FM27 with text export** with Steam open. Check `FM27-BepInEx/launch.log`, `fm27_doorstop.log`, and `BepInEx/LogOutput.log` if startup fails.
- **Unsupported screen:** open one player's Overview/Attributes, with English attribute panels visible, and capture again. Other screens and languages are not currently supported by the profile check.
- **Shortcut does nothing:** check the FM menu app is running and FM27 is in front. Try **Capture player profile** in the menu to check for a hotkey conflict.
- **Chat doesn't open:** use **Copy latest prompt for this chat**. Saved exports remain available.
- **Updating:** quit the companion, pull changes and rerun both install scripts. Restart the game when ready to load a new plugin. The plugin installer keeps old DLLs outside the active plugins directory.
- **Uninstalling:** quit the companion, remove its app and optionally its captures. Remove `BepInEx/plugins/FM27PlayerExport` to uninstall the plugin. Clear any loader Steam launch option to return to normal Steam launches.

---

# Original FM27 table exporter — macOS (Apple Silicon)

Export **Football Manager 27** player and staff tables to **CSV + HTML**. Load a view, select players, press **F9**.

It exports game UI text without reading save files or hidden player attributes. The loader runs a separate shadow copy of the game; the original game bundle remains unchanged.

**Credits / lineage:** original **FM26 Player Export** by **vinteset** → macOS fork by **DadMych** ([repo](https://github.com/DadMych/fm26-player-export-macos), MIT) → this **FM27 port**. Runs on **codexx01's FM27 BepInEx package**, based on sptndc's loader ([sortitoutsi thread](https://sortitoutsi.net/content/78080/fm27-bepinex-6-il2cpp-for-macos-apple-silicon-latest-macos-no-game-files-modified)).

**Maintained by:** [oeknudsen](https://github.com/oeknudsen)

---

## Requirements

- macOS on **Apple Silicon** (M1–M4), tested on macOS 27
- **Football Manager 27** (Steam)
- **codexx01's FM27 BepInEx package** (the mod loader — install this first): https://sortitoutsi.net/content/78080/fm27-bepinex-6-il2cpp-for-macos-apple-silicon-latest-macos-no-game-files-modified
- **Rosetta 2**

## Installation

1. Install **codexx01's FM27 BepInEx package** and set its Steam launch option. **Replace the `USERNAME` placeholder** in that launch option with your real macOS username.
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
- **F9 does nothing** → confirm the FM27 BepInEx loader loaded (a `BepInEx/LogOutput.log` exists) and that you pressed F9 on a Squad or Player Search screen.
- **Rows empty / only one column** → use a view with real attribute columns and keep hands off the mouse during scroll.

## Known limitations

- macOS Apple Silicon only (verified there).
- Star-rating columns (Ability / Potential) are not yet verified on FM27; text and attribute columns are confirmed working.

---

MIT License (see `LICENSE`). Football Manager is a trademark of Sports Interactive / SEGA; this project is not affiliated with or endorsed by them.
