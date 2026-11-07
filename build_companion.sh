#!/bin/bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
DESTINATION="${1:-$HERE/dist}"
mkdir -p "$DESTINATION"
DESTINATION="$(cd "$DESTINATION" && pwd)"
swift build --package-path "$HERE/companion" -c release
BIN_DIR="$(swift build --package-path "$HERE/companion" -c release --show-bin-path)"
APP="$DESTINATION/FM Profile Bridge.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN_DIR/FMProfileBridge" "$APP/Contents/MacOS/FMProfileBridge"
cp "$HERE/companion/Info.plist" "$APP/Contents/Info.plist"
cp "$HERE/LICENSE" "$APP/Contents/Resources/LICENSE"
codesign --force --sign - "$APP"
echo "Built: $APP"
