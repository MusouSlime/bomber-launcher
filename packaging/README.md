# Packaging

## Portable Linux AppImage (recommended)

Bundles Python, PyGObject, GTK 4, libadwaita, typelibs, schemas, the Adwaita
icon theme, UnityPy and the artwork extractor. No host GTK/Python needed (glibc, X11/Wayland and GPU
drivers still come from the system, as with any AppImage).

```sh
python3 -m venv --system-site-packages .venv-build
.venv-build/bin/pip install pyinstaller UnityPy pillow cryptography
PYI=.venv-build/bin/pyinstaller ./packaging/build-appimage-portable.sh
# -> dist/BomberLauncher-portable-x86_64.AppImage
```

Settings and backups are written to `BomberLauncher.data/` next to the
AppImage, so the pair is fully portable.

## Thin Linux AppImage

```sh
./packaging/build-appimage.sh
# -> dist/BomberLauncher-x86_64.AppImage
```

The thin AppImage: it bundles the launcher, its generated intro clip and
icon, but uses the host's `python3-gi`, GTK 4 and libadwaita 1.4+ (standard on
Bazzite, Fedora, Ubuntu 24.04+, Arch, ...). It deliberately ships no Konami
artwork; the interface falls back to a clean text/theme-icon layout.

To use the game-derived art, extract it on the machine and point the app at it:

```sh
BOMBER_ASSETS="$HOME/.local/share/bomber-launcher/assets" \
  .venv/bin/python tools/extract_ui_assets.py
BOMBER_ASSETS="$HOME/.local/share/bomber-launcher/assets" \
  ./dist/BomberLauncher-x86_64.AppImage
```

## Windows

A native WinForms port lives in `windows/BomberLauncher` and builds a
self-contained `BomberLauncher-win-x64.exe` without Python or GTK (see
`windows/README.md`). The GTK app itself cannot be cross-built by PyInstaller
from Linux; the older options were:

1. **CI build of this app** on a Windows runner with MSYS2
   (`python-gobject`, `gtk4`, `libadwaita`, PyInstaller). Same UI, but bundling
   GTK typelibs/DLLs/schemas is involved and needs iteration.
2. **Native port** (C#/WinForms or Qt) — a separate UI implementation, but a
   single self-contained `.exe` with no Python/GTK dependency.

The launcher's file operations (DLL patch, intro clip swap, save language,
BepInEx config) are identical on Windows once the Steam path is resolved.
