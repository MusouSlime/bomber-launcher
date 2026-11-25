# SBR2 QoL (BepInEx 6 IL2CPP)

Initial SBR 2 plugin with:

- **QoL:** skip logos, skip movies (key or auto), disable HDRP motion blur /
  depth of field / bloom / vignette / film grain / chromatic aberration / lens
  distortion, FOV override.
- **Custom BGM:** play `.ogg` / `.wav` / `.mp3` from a folder, matched by BGM
  label, with playlist/shuffle fallback.
- **Story character select:** choose the story player from the 38 SBR 2
  character keys instead of being locked to White.

Character porting from SBR 1 is intentionally not in this first plugin.

> This is a scaffold written against the SBR 2 class/method names from the
> IL2CPP dump. The source has been type-checked against the Il2CppDumper dummy
> assemblies + BepInEx 6 core, but the final DLL must be built after BepInEx
> generates the real interop assemblies on the first game launch. If a method
> name/overload differs, the patch is logged and skipped by `PatchAllSafe`;
> check `BepInEx/LogOutput.log`.

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

## Config

### Movies

- `SkipEnabled` — allow the skip key.
- `SkipKey` — Unity `KeyCode`, default `Space`.
- `SkipLogos` — skip the publisher/logo splash.
- `AutoSkipMovies` — seconds after which any playing movie is skipped (0 = off).

### Graphics

- `DisableMotionBlur`, `DisableDepthOfField`, `DisableBloom`,
  `DisableVignette`, `DisableFilmGrain`, `DisableChromaticAberration`,
  `DisableLensDistortion` — toggle HDRP `Volume` components by type name.
- `FieldOfView` — gameplay camera FOV; 0 leaves vanilla.

### Audio

- `CustomBgmFolder` — folder containing tracks. Empty disables replacement.
- `CustomBgmVolume` — 0.0–1.0.
- `CustomBgmShuffle` — random track when a label has no exact file.
- `CustomBgmPlaylist` — cycle tracks when a label has no exact file.

File matching:

- exact label: `BGM_sty_stg_01_c.ogg`
- base label: `BGM_sty_stg_01.ogg` matches `_a` / `_b` / `_c`
- otherwise, if shuffle/playlist is on, use a random/next track.

SBR 2 BGM labels start with `BGM_` or `snd_bgm_`.

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
- Logo skip patches a private nested `EntryScene.LogoManage`; if a patch is not
  found it logs a warning and the rest of the plugin still loads.
- HDRP toggles find `Volume` components at runtime by type name, so they survive
  scene/profile reloads but are not tied to a specific quality preset.
- Do not use gameplay/story mods in online crossplay or ranked.
