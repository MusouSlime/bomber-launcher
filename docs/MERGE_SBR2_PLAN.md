# SBR2 merge plan — “ultimate version of both games”

Status: architecture plan. No code has been merged yet.

## What “merge” can mean

There are three different merges, with very different costs:

1. **One launcher/repo for both games** — shared UI, config, install checks,
   save backup, artwork, audio tools; separate plugins. Low risk.
2. **SBR2 feature parity with SBR1** — port the QoL/randomizer/practice
   experience to SBR2. Medium to high.
3. **SBR1 content inside SBR2** — maps, characters, story, audio. Very high;
   an asset-conversion project, not a code merge.

Recommendation: treat **SBR2 as the primary “ultimate” package**, keep SBR1 as
a supported legacy target, and do the merges in that order.

## Why the code cannot be merged directly

| | SBR1 | SBR2 |
|---|---|---|
| Unity | 5.6.5 | 2020.3.36f1 |
| Scripting | Mono, x86 | IL2CPP, x64 |
| Loader | BepInEx 5 | BepInEx 6 IL2CPP |
| Renderer | Built-in/legacy | HDRP + Fuse |
| Assets | AES UnityFS bundles | 0x100-header AES UnityFS bundles |
| Online | Steam lobbies + Steam P2P | Unity Multiplay/Relay + Steam/crossplay |

A single plugin DLL cannot target both. Two plugin builds are required, selected
by a shared launcher.

## Phase 0 — repository and launcher merge

1. Restructure into clear targets:
   ```text
   sbr1/        plugin, patches, SBR1 tools
   sbr2/        plugin, patches, SBR2 tools
   launcher/    shared GTK/WinForms UI
   tools/       shared crypto/audio/art helpers
   docs/
   ```
2. One launcher with a **game selector**:
   - detect SBR1 (`702700`) and SBR2 (`1913740`)
   - per-game BepInEx 5/6 install/update
   - per-game plugin deployment and config paths
   - shared save backup, artwork extraction, audio conversion
   - per-game feature tabs, not a single config union
3. CI builds:
   - SBR1 plugin: BepInEx 5 x86
   - SBR2 plugin: BepInEx 6 IL2CPP x64
   - launcher artifacts for both games
4. Bring the current untracked SBR2 scaffold, tools, patches and docs under
   version control after review.

**Gate:** SBR1 remains at its current 0.6.11 behavior; SBR2 basic QoL loads with
real BepInEx 6 interop.

## Phase 1 — port the core SBR1 experience to SBR2

Feature parity matrix:

| Feature | SBR2 status | Work |
|---|---|---|
| Skip logos/movies | Partial | Validate real IL2CPP targets |
| HDRP post-FX toggles | Partial | Add high-end/floor presets |
| Low-End / Ultra-Low floor | Missing | Port tier model to HDRP/QualitySettings |
| High-End ceiling | Missing | Port native res, max HDRP quality |
| Custom BGM | Done (scaffold) | Test, add playlist/volume UI |
| Custom SFX | Missing | Port label-based SFX replacement |
| FOV/ultrawide | Partial | Camera patch + launcher aspect |
| Japanese voices | Data/config | Wire existing `_j` voice packs |
| Fast results/rematch | Missing | Patch result flow/play-again |
| Save backup | Launcher | Shared launcher |
| F9 overlay / main-menu entry | Missing | IL2CPP IMGUI overlay |
| Practice/randomizer | Missing | New implementation for SBR2 flow |

**Gate:** SBR2 has the same performance tiers, skip behavior, audio, save
backup and an in-game QoL menu.

## Phase 2 — SBR2-specific “ultimate” content

Use existing SBR2 assets and data first:

- expose all 24 Standard maps, both Battle 64 maps, Basic/Crystal maps
- expose existing rule toggles (skull, specials, outside attack, pressure
  blocks, revenge carts, set count, start position)
- COM strength and team select in offline/private rooms
- unlock existing characters/costumes/accessories/icons through release data
- story EXP/penalty tuning, Castle fixes, faster transitions
- AI fixes (danger avoidance, bomb vision, search range)
- editor limits/templates
- bot backfill/private lobbies for the dead online modes

Keep all gameplay mods offline/private. SBR2 uses Unity Relay and crossplay;
custom gameplay in public online is a ToS/desync risk.

## Phase 3 — SBR1 content in SBR2

This is the “ultimate version” content question, and it is separate from the
plugin merge:

- SBR1 maps are Unity 5.6 assets with built-in/legacy materials.
- SBR2 is Unity 2020.3 HDRP.
- Characters need rig/avatar/animation/material conversion.
- Story flow, UI, voice and save data are different.
- Asset distribution must remain patch-only; do not ship Konami assets.

A first experiment would be a **single SBR1 map rebuilt as an SBR2 custom
stage**, not a bulk port. Treat bulk story/character porting as a separate
multi-month asset project.

## Recommended order

1. Commit the SBR2 scaffold/tools/docs and verify the SBR2 plugin on the real
   IL2CPP interop assemblies.
2. Refactor the launcher into a two-game launcher with separate plugin targets.
3. Port performance tiers (floor/ceiling), skip, audio, save backup and F9 UI
   to SBR2.
4. Port the SBR1 randomizer/practice concepts where SBR2 APIs allow.
5. Add SBR2-specific existing-content unlocks and fixes.
6. Re-evaluate a single SBR1 map port to SBR2 before promising character/story
   content.

## Definition of “ultimate”

For the first release, “ultimate” should mean:

- **SBR2 is the primary game and gets the modern QoL suite.**
- **SBR1 remains a fully supported legacy target** in the same launcher.
- SBR1 content is not promised inside SBR2 until the asset pipeline proves one
  map/character end-to-end.

## Decisions needed

1. Is the first merge a **launcher/repo merge** (recommended), or must SBR2
   feature parity ship in the same release?
2. Should SBR1 remain a supported target, or be frozen at 0.6.11?
3. Should SBR2 use a BepInEx 6 F9 overlay, or launcher-only configuration first?
4. Is a single SBR1 map port to SBR2 worth prototyping before other content?
5. Where do the SBR2 tools/patches live: same repo or a sibling project?

## Phase 0 progress — 2026-11-25

Completed:

- SBR2 scaffold, tools, patches and docs are now under version control.
- BepInEx 6 IL2CPP x64 is installed into the SBR2 folder.
- SBR2 plugin now compiles against the real generated interop assemblies
  (`GameAssembly`, `HxSystemAssembly`, `Il2Cppmscorlib`, `Il2CppSystem`).
- Fixed the modern IL2CPP API differences:
  - `Il2CppType.Of<T>()` for `Resources.FindObjectsOfTypeAll`
  - `BepInEx.Unity.IL2CPP.Utils` coroutine extension
  - `UnityWebRequestMultimedia` instead of `WWW`

Blocker found:

- BepInEx 6 be.692 and be.793 both fail managed **ClassInjector** on this SBR2
  build:
  - `Sequence contains no elements` in
    `MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook`
  - adding an injected `MonoBehaviour` to a `GameObject` then crashes with
    `System.AccessViolationException`
- With `BepInEx/config/BepInEx.cfg` → `UnityLogListening = false`, the
  chainloader works. The SBR2 plugin now loads as a **Harmony-only** plugin:
  - logo/story patches register
  - `QoLHost` and `CustomBgmPlayer` are intentionally disabled
  - movie-skip/post-FX/FOV runtime loops are not active yet

Next decision:

1. **MelonLoader** for SBR2 (better managed-host support), or
2. **BepInEx Harmony-only SBR2 plugin** — implement update loops through
   Harmony patches and create only built-in `AudioSource` components, no
   managed `MonoBehaviour` injection.

Until that is decided, SBR2 is not ready to be the primary “ultimate” runtime;
the SBR1 0.6.11 build remains the stable target.
