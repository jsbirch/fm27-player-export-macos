#!/bin/bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
DESTINATION="${1:-$HOME/Applications}"
if pgrep -x FMProfileBridge >/dev/null; then
    echo "Quit FM Profile Bridge from its FM menu before installing an update." >&2
    exit 1
fi
bash "$HERE/build_companion.sh" "$DESTINATION"
open "$DESTINATION/FM Profile Bridge.app"
