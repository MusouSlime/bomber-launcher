#!/usr/bin/env bash
# Timestamped save backup.
set -e
GAME="${SBR_GAME:-$HOME/.local/share/Steam/steamapps/common/Super Bomberman R}"
MODKIT="${SBR_MODKIT:-$HOME/Projects/sbr-modkit}"
DEST="$MODKIT/backups/saves"
mkdir -p "$DEST"
cp "$GAME/savedata.bin" "$DEST/savedata_$(date +%Y%m%d_%H%M%S).bin"
ls -lt "$DEST" | head -5
