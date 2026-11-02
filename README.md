# FM27 Player Export — macOS (Apple Silicon)

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
