#!/usr/bin/env bash
# Install BepInEx 5 (x86) and the BomberQoL plugin into the game folder.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${SBR_GAME:-$HOME/.local/share/Steam/steamapps/common/Super Bomberman R}"
VER="5.4.23.2"
ZIP="BepInEx_win_x86_${VER}.zip"
URL="https://github.com/BepInEx/BepInEx/releases/download/v${VER}/${ZIP}"

if [ ! -f "$GAME/SuperBombermanR.exe" ]; then
  echo "Game not found at: $GAME" >&2
  echo "Set SBR_GAME to the install directory." >&2
  exit 1
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
echo "Downloading BepInEx ${VER} (x86)..."
curl -fsSL -o "$TMP/$ZIP" "$URL"
unzip -oq "$TMP/$ZIP" -d "$GAME"
mkdir -p "$GAME/BepInEx/plugins"

if [ -f "$HERE/plugin/BomberQoL.dll" ]; then
  cp "$HERE/plugin/BomberQoL.dll" "$GAME/BepInEx/plugins/"
elif [ -x "$HERE/plugin/build.sh" ]; then
  "$HERE/plugin/build.sh"
  cp "$HERE/plugin/BomberQoL.dll" "$GAME/BepInEx/plugins/"
fi

cat <<'EOF'

BepInEx installed. Proton needs winhttp to resolve to BepInEx's DLL.
Use ONE of these:

1) Steam launch options for Super Bomberman R:
     WINEDLLOVERRIDES="winhttp=n,b" %command%

2) Wine registry override inside the game prefix:
     [Software\\Wine\\DllOverrides]
     "winhttp"="native,builtin"

Bomber Launcher writes BepInEx/config/bomber.qol.cfg when you toggle
cutscene skip or post-processing options. Uninstall by deleting winhttp.dll,
doorstop_config.ini, .doorstop_version and the BepInEx folder.
EOF
