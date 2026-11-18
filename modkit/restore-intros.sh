#!/usr/bin/env bash
# Restore the original 4-minute boot intro movies from the modkit backups.
set -e
GAME="${SBR_GAME:-$HOME/.local/share/Steam/steamapps/common/Super Bomberman R}"
MODKIT="${SBR_MODKIT:-$HOME/Projects/sbr-modkit}"
BIN="$GAME/SuperBombermanR_Data/StreamingAssets/bin"
for f in bom_00_intro_a bom_00_intro_b; do
  SRC="$MODKIT/backups/videos/$f"
  if [ -f "$SRC" ]; then
    cp -f "$SRC" "$BIN/$f"
    echo "restored $f"
  else
    echo "missing backup: $SRC (use Steam > Verify integrity)" >&2
  fi
done
