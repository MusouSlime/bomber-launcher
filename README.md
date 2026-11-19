# Bomber Launcher

QoL launcher for **Super Bomberman R** (Steam 702700) on Linux, styled with
artwork extracted from the game itself.

![Bomber Launcher](docs/screenshot.png)

## Features

| Control | Effect |
| --- | --- |
| **Text Language** | Writes the language byte in `savedata.bin` (all 12 shipped locales + Auto) |
| **Borderless Fullscreen** | Launches `steam -applaunch 702700 -popupwindow -screen-width … -screen-height …` |
| **Japanese Voices** | Patches `Assembly-CSharp.dll` so the voice suffix defaults to `_J`; text stays in the selected language |
| **Skip Boot Intro** | Replaces the two ~4-minute opening MP4s with a 1-second clip |
| **Skip Cutscenes** | BepInEx plugin: press Space during any movie |
| **Motion Blur / DoF / Bloom** | BepInEx plugin: per-effect post-processing switches |
| **Restore Original Files** | Reverts the DLL patch and intro movies from backups |
| **Back Up Save** | Timestamped copy of `savedata.bin` |

Toggles disable themselves while the game is running and every change is
reversible.

## Install

Requires `python3-gi`, GTK 4 and libadwaita 1.4+ (standard on Bazzite and most
modern desktops).

```sh
git clone https://github.com/MusouSlime/bomber-launcher.git
cd bomber-launcher
./install.sh            # menu entry + icon
```

Then rebuild the game artwork for the interface (optional, but the UI is blank
without it):

```sh
python3 -m venv .venv
.venv/bin/pip install UnityPy pillow cryptography
.venv/bin/python tools/extract_ui_assets.py
```

Launch from the application menu or:

```sh
python3 bomber_launcher.py
python3 bomber_launcher.py --check     # state without opening the window
```

Paths can be overridden with `SBR_GAME` and `SBR_MODKIT`.

## Optional: BepInEx code mods

```sh
./install-bepinex.sh
```

Then make Proton load BepInEx's `winhttp.dll`, either with the Steam launch
option:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

or by adding `"winhttp"="native,builtin"` under
`[Software\\Wine\\DllOverrides]` in the game's prefix `user.reg`.

The plugin adds:

- **Skip Cutscenes** — press `Space` while any movie is playing (includes the
  boot intro).
- **Motion Blur / Depth of Field / Bloom** — independent switches.

Both are exposed in the launcher and stored in
`BepInEx/config/bomber.qol.cfg`; changes apply on the next launch. Plugin
source is in `plugin/` (`./plugin/build.sh`).

## Artwork and legal

No Konami assets are stored in this repository. `tools/extract_ui_assets.py`
decrypts the game's own UnityFS packs and rebuilds the banner, avatars, menu
wordmarks and bars into `assets/` on your machine. The only committed media is
`assets/intro_blank.mp4` (a generated 1-second black clip).

Super Bomberman R and its artwork are © Konami. This is an unofficial,
non-commercial quality-of-life tool and is not affiliated with Konami or
HexaDrive. The screenshot shows in-game artwork.

## How it works

- **Dual audio:** the single `#US` string literal `_E` in
  `Assembly-CSharp.dll` becomes `_J` (same length). The default returned by
  `LocaleManager.getVoiceLabelSuffix()` changes for every non-Japanese text
  language, which switches both the loaded `sound/**/*_j` packs and the
  `V_*_J` / `SND_MOV_*_J` clip names. The JP chara pack covers all 1,951 voice
  lines and all 23 movie audio packs have `_j` twins.
- **Language:** `savedata.bin` is an xorshift32-XOR container with MD5-hashed
  chunks. The launcher decodes it, edits `SystemData.language` (offset 1132 of
  the 1352-byte blob), re-hashes and re-encrypts. Unmodified round-trips are
  byte-identical; each write keeps a timestamped backup.
- **Intro skip:** originals move to the modkit backups; the bundled 1080p30
  one-second clip is copied into `StreamingAssets/bin/`.
- **Packs:** AES-128-CBC UnityFS bundles, key derived from the file name via
  PBKDF2-HMAC-SHA1 (1103 iterations). See `tools/sbr_pack.py`.

## Safety

- Close the game before toggling mods or changing language.
- Steam → *Properties → Installed Files → Verify integrity* restores vanilla
  files and removes the mods; re-apply afterwards.
- If Steam Cloud overwrites an older `savedata.bin`, set the language again
  with the game closed.
- Backups live in `~/Projects/sbr-modkit` by default (override with
  `SBR_MODKIT`).

## Layout

```
bomber_launcher.py        GTK4/libadwaita app
install.sh                menu entry + icon
assets/                   generated from the game (gitignored)
docs/screenshot.png
modkit/                   CLI fallbacks (dual audio, intros, save, pack crypto)
tools/extract_ui_assets.py
tools/sbr_pack.py
plugin/                    BepInEx plugin source + prebuilt DLL
install-bepinex.sh
```

## License

MIT for the code (see `LICENSE`). Game assets remain Konami's.
