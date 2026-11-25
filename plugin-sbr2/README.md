# SBR2 QoL (BepInEx 6 IL2CPP)

Initial SBR 2 plugin with:

- **QoL:** skip movies (key or auto), disable HDRP motion blur / depth of
  field / bloom / vignette / film grain / chromatic aberration / lens
  distortion, FOV override.
- **Custom BGM:** play `.ogg` / `.wav` from a folder, matched by BGM label,
  with playlist/shuffle fallback. Uses the launcher's `bgm_cache` directly;
  no WAV conversion or injected MonoBehaviour is needed.
- **Story character select:** choose the story player from the 38 SBR 2
  character keys instead of being locked to White.

Character porting from SBR 1 is intentionally not in this first plugin.

> Built against the real BepInEx 6 interop assemblies. Patch failures are
> logged by `PatchAllSafe`; check `BepInEx/LogOutput.log` if a feature stops
> working after a game update.

## Prerequisites

- BepInEx **6 bleeding edge, x64 IL2CPP** (not BepInEx 5 x86).
  Builds: <https://builds.bepinex.dev/projects/bepinex_be>
- Run SBR 2 once with BepInEx installed so `BepInEx/interop` is generated.
- .NET SDK 6+ for building.

## Install

```sh
# extract BepInEx 6 into the game folder first
export SBR2_GAME="$HOME/.local/share/Steam/steamapps/common/SUPER BOMBERMAN R2"
./build.sh
```

Proton launch option:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

The config is written to `BepInEx/config/sbr2.qol.cfg`.

## In-game overlay

Press **F9** in game for a Harmony-only settings overlay. It is drawn with
built-in uGUI components, so no managed `MonoBehaviour` is injected.

- Up/Down selects a row; Left/Right changes it; Enter also advances.
- Controller: left stick or D-pad moves, A changes, B / Start / Back closes.
- F9 closes and saves to `BepInEx/config/sbr2.qol.cfg`.
- With `ReplaceOptionsMenu = true`, the main-menu **gear** and the
  **Basic Settings** entry open this overlay instead of the vanilla panels.
  Hold **Shift** while selecting them to reach the vanilla menu.
- The game also sees the same keys, so the menu behind can move while the
  overlay is open. Use it in a menu rather than mid-battle.

It covers BGM volume/shuffle/playlist, the HDRP post-FX toggles, FOV, movie
skip settings and the story character. The BGM folder is chosen in the
launcher (the overlay only manages values).

Config:

```
[Interface]
OverlayEnabled = true
OverlayKey = F9
ReplaceOptionsMenu = true
```

## Config

### Movies

- `SkipEnabled` — allow the skip key.
- `SkipKey` — Unity `KeyCode`, default `Space`.
- `SkipLogos` — disabled; the old `beginLogo` hook left the game on a black
  screen. Skip needs a different entry-scene hook.
- `AutoSkipMovies` — seconds after which any playing movie is skipped (0 = off).

### Graphics

- `DisableMotionBlur`, `DisableDepthOfField`, `DisableBloom`,
  `DisableVignette`, `DisableFilmGrain`, `DisableChromaticAberration`,
  `DisableLensDistortion` — toggle the active HDRP volume-stack components.
  These are read through `VolumeManager.instance.stack`; the game is never
  scanned with `Resources.FindObjectsOfTypeAll` (that froze SBR2 at the
  caution screen).
- `FieldOfView` — gameplay camera FOV; 0 leaves vanilla.

### Audio

- `CustomBgmFolder` — folder containing `.ogg` / `.wav` tracks. Point this at
  the launcher's `bgm_cache`; OGG is decoded with NVorbis and PCM WAV with the
  built-in parser. Empty disables replacement.
- `CustomBgmVolume` — 0.0–1.0.
- `CustomBgmShuffle` — random track when a label has no exact file.
- `CustomBgmPlaylist` — cycle tracks when a label has no exact file.

File matching:

- exact label: `BGM_sty_stg_01_c.ogg`
- base label: `BGM_sty_stg_01.ogg` matches `_a` / `_b` / `_c`
- otherwise, if shuffle/playlist is on, use a random/next track.

SBR 2 BGM labels start with `BGM_` or `snd_bgm_`.

### Debug

- `SelfTest` — play one custom track shortly after startup and log
  `playing=True/False`. Leave off for normal play.

### Story

- `StoryCharacter` — one of:

```
white black pink blue red yellow green aqua
simon pyramid richter lance bill mimi nyami alucard
vicviper nurse lobby tomato nakedSnake raiden oldsnake
vicviperzero somaCruz fujisaki genso2p goemon
magnet tinyGolem phantom sexy plasma
fiter Massive wing bean lucaBlight
```

Empty = vanilla White. The hook changes `StoryManager.getStoryCharacterId`,
which is used by all story battle parameter creators (explore, boss, Castle,
base defence, last battle).

## Known limitations

- Story movies/cutscenes still show White; gameplay uses the selected character.
- Story stats/voice may still follow White depending on the story param path;
  test `white` → `pink`/`simon` first.
- Custom BGM is played through an external `AudioSource`; `SoundManager`’s BGM
  stack/spectrum APIs may not reflect it. `popBGM` stops the custom track and
  lets the original stack restore.
- Logo skip is disabled; patching `EntryScene.LogoManage.beginLogo` left the
  game on a black screen.
- HDRP toggles disable the active volume-stack components, so they apply to the
  current quality preset without touching saved profiles.
- Do not use gameplay/story mods in online crossplay or ranked.
