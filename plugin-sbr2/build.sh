#!/usr/bin/env bash
# Build SBR2QoL.dll against the BepInEx 6 interop assemblies.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${SBR2_GAME:-$HOME/.local/share/Steam/steamapps/common/SUPER BOMBERMAN R2}"
BEP="$GAME/BepInEx"

if [ ! -d "$BEP/interop" ]; then
  cat >&2 <<EOF
BepInEx 6 interop assemblies not found at:
  $BEP/interop

Install BepInEx 6 (x64 IL2CPP) into the SBR2 folder, run the game once so
BepInEx generates the interop assemblies, then rerun this script.
EOF
  exit 1
fi

mkdir -p "$HERE/bin" "$BEP/plugins"
dotnet build "$HERE/SBR2QoL.csproj" \
  -c Release \
  -p:BepInExDir="$BEP" \
  -o "$HERE/bin"

cp "$HERE/bin/SBR2QoL.dll" "$BEP/plugins/"
[ -f "$HERE/bin/SBR2QoL.pdb" ] && cp "$HERE/bin/SBR2QoL.pdb" "$BEP/plugins/" || true

echo "Installed: $BEP/plugins/SBR2QoL.dll"
echo "Config:    $BEP/config/sbr2.qol.cfg"
