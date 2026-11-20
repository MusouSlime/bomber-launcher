#!/usr/bin/env bash
# Build a self-contained AppImage: Python, PyGObject, GTK4, libadwaita and the
# app are bundled. Requires a PyInstaller that can see the system PyGObject:
#   python3 -m venv --system-site-packages .venv-build
#   .venv-build/bin/pip install pyinstaller
#   PYI=.venv-build/bin/pyinstaller ./packaging/build-appimage-portable.sh
set -e
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"
PYI="${PYI:-$(command -v pyinstaller)}"
BUILD="$HERE/build-portable"
APPDIR="$HERE/AppDir-portable"
TOOL="$HERE/appimagetool"
DIST="$ROOT/dist"

[ -x "$PYI" ] || { echo "PyInstaller not found; set PYI=..." >&2; exit 1; }
SCHEMAS="/usr/share/glib-2.0/schemas/gschemas.compiled"
[ -f "$SCHEMAS" ] || { echo "missing $SCHEMAS (glib2)" >&2; exit 1; }

rm -rf "$BUILD" "$APPDIR"
mkdir -p "$BUILD" "$DIST"

"$PYI" --noconfirm --clean --windowed --onedir --name BomberLauncher \
  --distpath "$BUILD/dist" --workpath "$BUILD/work" --specpath "$BUILD" \
  --runtime-hook "$HERE/rthook_portable.py" \
  --add-data "$ROOT/assets/intro_blank.mp4:assets" \
  --add-data "$ROOT/plugin/BomberQoL.dll:plugin" \
  --add-data "$ROOT/tools/extract_ui_assets.py:tools" \
  --collect-all UnityPy \
  --hidden-import PIL --hidden-import cryptography \
  --add-data "$SCHEMAS:share/glib-2.0/schemas" \
  --add-data "/usr/share/icons/Adwaita:share/icons/Adwaita" \
  --hidden-import gi --hidden-import gi.repository.Gtk \
  --hidden-import gi.repository.Adw --hidden-import gi.repository.Gdk \
  "$ROOT/bomber_launcher.py" >/dev/null

mkdir -p "$APPDIR/usr/share/bomber-launcher"
cp -a "$BUILD/dist/BomberLauncher/." "$APPDIR/usr/share/bomber-launcher/"
cp "$HERE/bomber-launcher.desktop" "$APPDIR/"
mkdir -p "$APPDIR/usr/share/metainfo"
cp "$HERE/io.github.bomberlauncher.BomberLauncher.appdata.xml" \
   "$APPDIR/usr/share/metainfo/"
cp "$HERE/AppRun-portable" "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun"

if command -v rsvg-convert >/dev/null 2>&1; then
  rsvg-convert -w 256 -h 256 "$HERE/bomber-launcher.svg" -o "$APPDIR/bomber-launcher.png"
else
  magick "$HERE/bomber-launcher.svg" -resize 256x256 "$APPDIR/bomber-launcher.png"
fi
cp "$APPDIR/bomber-launcher.png" "$APPDIR/.DirIcon"

if [ ! -x "$TOOL" ]; then
  curl -fsSL -o "$TOOL" \
    https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage
  chmod +x "$TOOL"
fi

ARCH=x86_64 "$TOOL" --appimage-extract-and-run "$APPDIR" \
  "$DIST/BomberLauncher-portable-x86_64.AppImage"

echo "built $DIST/BomberLauncher-portable-x86_64.AppImage"
