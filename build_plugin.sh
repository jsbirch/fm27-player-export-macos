#!/bin/bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
FM27_BEP="${FM27_BEP:-$HOME/Library/Application Support/Steam/steamapps/common/Football Manager 27/FM27-BepInEx/BepInEx}"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
dotnet build "$HERE/plugin/FM27PlayerExport.csproj" -c Release \
  -p:CoreDir="$FM27_BEP/core" \
  -p:InteropDir="$FM27_BEP/interop"
OUT="$HERE/plugin/bin/Release/netstandard2.1/FM27PlayerExport.dll"
cp "$OUT" "$HERE/FM27PlayerExport.dll"
echo "Built: $OUT"
echo "Copied to: $HERE/FM27PlayerExport.dll"
