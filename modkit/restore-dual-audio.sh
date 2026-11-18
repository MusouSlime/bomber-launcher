#!/usr/bin/env bash
# Revert the dual-audio patch from its backup.
set -e
GAME="${SBR_GAME:-$HOME/.local/share/Steam/steamapps/common/Super Bomberman R}"
MODKIT="${SBR_MODKIT:-$HOME/Projects/sbr-modkit}"
DEST="$GAME/SuperBombermanR_Data/Managed/Assembly-CSharp.dll"
ORIG="$MODKIT/backups/Assembly-CSharp.dll.orig"
[ -f "$ORIG" ] || { echo "backup not found: $ORIG" >&2; exit 1; }
cp -f "$ORIG" "$DEST"
echo "restored original Assembly-CSharp.dll"
