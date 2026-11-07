#!/bin/bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
FM27_BEP="${FM27_BEP:-$HOME/Library/Application Support/Steam/steamapps/common/Football Manager 27/FM27-BepInEx/BepInEx}"
if [ ! -f "$FM27_BEP/core/BepInEx.Unity.IL2CPP.dll" ]; then
    echo "Install the FM27 BepInEx loader first, or set FM27_BEP to its BepInEx directory." >&2
    exit 1
fi
DESTINATION="$FM27_BEP/plugins/FM27PlayerExport"
mkdir -p "$DESTINATION"
if [ -f "$DESTINATION/FM27PlayerExport.dll" ]; then
    BACKUP="$FM27_BEP/plugin-backups/$(date '+%Y-%m-%d_%H-%M-%S')-$$"
    mkdir -p "$BACKUP"
    cp "$DESTINATION/FM27PlayerExport.dll" "$BACKUP/"
fi
cp "$HERE/FM27PlayerExport.dll" "$DESTINATION/FM27PlayerExport.dll.new"
mv "$DESTINATION/FM27PlayerExport.dll.new" "$DESTINATION/FM27PlayerExport.dll"
echo "Installed: $DESTINATION/FM27PlayerExport.dll"
echo "Takes effect next time FM27 starts through the BepInEx loader. A running game is left alone."
