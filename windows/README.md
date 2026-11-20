# Bomber Launcher for Windows

Native WinForms port of the Linux launcher. Single self-contained executable:
no Python, GTK or .NET runtime needed.

## Download

`BomberLauncher-portable-win-x64.zip` from the GitHub release. Extract anywhere
and run `BomberLauncher.exe`. Windows 10/11 x64; no installer, no .NET runtime.

Settings and backups are written to `data\` next to the exe when that folder is
writable, otherwise `%LOCALAPPDATA%\BomberLauncher`.

## Features

- Steam library detection (registry + `libraryfolders.vdf`)
- Text language (all 12 locales + Auto)
- Borderless launch via `-popupwindow`
- Japanese voices (dual audio) — DLL patch with backup
- Skip boot intro — one-second clip, originals backed up
- BepInEx config toggles: skip cutscenes, motion blur, depth of field, bloom
- Cutscene skip works from Space or controller button (B/Circle default;
  `SkipJoystickButton` in the plugin config)
- Restore originals, save backup, open backup folder

Backups live in `data\backup` in portable mode.

The exe ships with no Konami artwork. If you have extracted the art from the
Linux repo (`assets/hero.png`), put an `assets` folder next to the exe to use it
as the header image.

## BepInEx (optional, for cutscene skip and post-FX)

1. Download `BepInEx_win_x86_5.4.23.2.zip` from the BepInEx releases page.
2. Extract it into the game folder (next to `SuperBombermanR.exe`).
3. Copy `plugin/BomberQoL.dll` from this repo into `BepInEx/plugins/`.
4. Start the game once; `BepInEx/config/bomber.qol.cfg` is created.
5. The launcher then edits that config. Changes apply on the next launch.

No `WINEDLLOVERRIDES` is needed on Windows.

## Build

Requires the .NET SDK (10.0) on Linux or Windows:

```sh
dotnet publish windows/BomberLauncher/BomberLauncher.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The exe lands in
`windows/BomberLauncher/bin/Release/net10.0-windows/win-x64/publish/`.

Cross-compiling from Linux works with `EnableWindowsTargeting=true` (already
set). The release build was smoke-tested under Wine 11.
