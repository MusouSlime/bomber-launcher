#!/usr/bin/env bash
# Launch Super Bomberman R borderless at the current desktop resolution.
exec steam -applaunch 702700 -popupwindow \
  -screen-width "${SBR_WIDTH:-1920}" -screen-height "${SBR_HEIGHT:-1080}" "$@"
