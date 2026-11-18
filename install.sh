#!/usr/bin/env bash
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
APPS="$HOME/.local/share/applications"
ICONS="$HOME/.local/share/icons/hicolor/256x256/apps"
mkdir -p "$APPS" "$ICONS"
ICON=applications-games
if [ -f "$HERE/assets/logo_square.png" ]; then
  cp "$HERE/assets/logo_square.png" "$ICONS/bomber-launcher.png"
  ICON=bomber-launcher
fi
cat > "$APPS/bomber-launcher.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Bomber Launcher
Comment=QoL launcher for Super Bomberman R
Exec=/usr/bin/python3 $HERE/bomber_launcher.py
Icon=$ICON
Terminal=false
Categories=Game;
StartupWMClass=io.github.bomberlauncher.BomberLauncher
DESKTOP
update-desktop-database "$APPS" 2>/dev/null || true
echo "Installed Bomber Launcher to $APPS"
echo "Rebuild artwork with: .venv/bin/python tools/extract_ui_assets.py"
