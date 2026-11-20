#!/usr/bin/env bash
# Build a thin AppImage. GTK4/libadwaita/python3-gi come from the host.
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
APPDIR="$HERE/AppDir"
DIST="$ROOT/dist"
TOOL="$HERE/appimagetool"

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/share/bomber-launcher/assets" "$DIST"

cp "$ROOT/bomber_launcher.py" "$APPDIR/usr/share/bomber-launcher/"
cp "$ROOT/assets/intro_blank.mp4" "$APPDIR/usr/share/bomber-launcher/assets/"
cp "$ROOT/LICENSE" "$APPDIR/usr/share/bomber-launcher/"
cp "$HERE/AppRun" "$APPDIR/AppRun"
cp "$HERE/bomber-launcher.desktop" "$APPDIR/"
chmod +x "$APPDIR/AppRun"

if command -v rsvg-convert >/dev/null 2>&1; then
  rsvg-convert -w 256 -h 256 "$HERE/bomber-launcher.svg" -o "$APPDIR/bomber-launcher.png"
else
  magick "$HERE/bomber-launcher.svg" -resize 256x256 "$APPDIR/bomber-launcher.png"
fi
cp "$APPDIR/bomber-launcher.png" "$APPDIR/.DirIcon"

if [ ! -x "$TOOL" ]; then
  echo "Downloading appimagetool..."
  curl -fsSL -o "$TOOL" \
    https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
  chmod +x "$TOOL"
fi

ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" \
  "$DIST/BomberLauncher-x86_64.AppImage"

echo "built $DIST/BomberLauncher-x86_64.AppImage"
