#!/usr/bin/env bash
# Build BomberQoL.dll against the game's own Unity assemblies.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${SBR_GAME:-$HOME/.local/share/Steam/steamapps/common/Super Bomberman R}"
MAN="$GAME/SuperBombermanR_Data/Managed"
BEP="$GAME/BepInEx/core"

mcs -nostdlib -target:library -out:"$HERE/BomberQoL.dll" \
  -r:"$MAN/mscorlib.dll" -r:"$MAN/System.dll" -r:"$MAN/System.Core.dll" \
  -r:"$BEP/BepInEx.dll" -r:"$BEP/0Harmony.dll" \
  -r:"$MAN/UnityEngine.dll" -r:"$MAN/Assembly-CSharp.dll" \
  "$HERE/BomberQoL.cs"

echo "built $HERE/BomberQoL.dll"
