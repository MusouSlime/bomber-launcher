# Packaging

## Linux AppImage

```sh
./packaging/build-appimage.sh
# -> dist/BomberLauncher-x86_64.AppImage
```

The AppImage is **thin**: it bundles the launcher, its generated intro clip and
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

There is no Windows binary yet. This app is Python + PyGObject + GTK4 +
libadwaita, and PyInstaller cannot cross-compile from Linux. A Windows exe needs
one of:

1. **CI build of this app** on a Windows runner with MSYS2
   (`python-gobject`, `gtk4`, `libadwaita`, PyInstaller). Same UI, but bundling
   GTK typelibs/DLLs/schemas is involved and needs iteration.
2. **Native port** (C#/WinForms or Qt) — a separate UI implementation, but a
   single self-contained `.exe` with no Python/GTK dependency.

The launcher's file operations (DLL patch, intro clip swap, save language,
BepInEx config) are identical on Windows once the Steam path is resolved.
