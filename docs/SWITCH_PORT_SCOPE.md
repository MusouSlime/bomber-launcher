# Switch NSP port / modding scope

Status: scoping only. No NSPs or extracted game assets are committed to this
repository. Keep keys, NSPs and extracted content local.

## Files inspected

| File | Status |
|---|---|
| `Super-Bomberman-R-Switch-NSP-Base-Game.rar` | **Valid.** 1.9 GB NSP, title `01007AD00013E000`, v0 |
| `Super-Bomberman-R-Switch-NSP-Update-2.2.rar` | **Valid.** 2.6 GB NSP, title `01007AD00013E800`, v720896 |
| `SUPER BOMBERMAN R 2 Switch NSP Update v393216.rar` | **Valid update.** 339 MB, title `0100B87017D94800`, v393216 |
| `SUPER-BOMBERMAN-R-2-Switch-NSP-Base-Game(1).rar` | **Valid.** 4.8 GB NSP, title `0100B87017D94000`, v0 |
| `SUPER-BOMBERMAN-R-2-Switch-NSP-Base-Game.rar` | 0 bytes (superseded by the `(1)` download) |
| `GameCube - Viewtiful Joe ... .zip` | Ignored for this scope |

Keys are present at `~/.switch/prod.keys` and `~/.switch/title.keys`.

## What we verified

- `nstool 1.9.2` extracts the NSP containers and NCA headers.
- **SBR1 base** extracts cleanly: ExeFS + **1.9 GB RomFS**.
- SBR1 RomFS layout matches the PC build:
  ```text
  Data/StreamingAssets/InAppAssets/
    common_pack, common_battle_pack
    chara/chara_pack_01
    map/data, map/env, map/res, map/thumbnail
    sound/bgm, define, enemy, block, acce, ...
  ```
- Unity **5.6** asset bundles. UnityPy loads the Switch bundles directly
  (`common_pack`: 351 objects, `chara_pack_01`: 2,608 objects, including
  `model_pc001_white` and a separate face mesh).
- Base SBR1 v0 contains only the **15 base characters** (`white` … `plasma`).
  The crossover/Nintendo roster is added by the update.
- SBR1 update 2.2 uses **DeltaFragment NCAs**; the program NCA's RomFS is not
  directly readable with `nstool --basenca`. A delta-aware extractor
  (`hactool` / Ryujinx RomFS dump) is required to see the update-only assets.
- **SBR2 base** extracts cleanly: 4.8 GB RomFS.
  - `Data/StreamingAssets/InAppAssets/Switch/` uses the same categories as the
    PC `Win64` build (`chara`, `defines`, `map`, `costume`, `sounds`, ...).
  - Unity **2020.3.36f1**, same as PC.
  - Unlike PC, the Switch payload bundles are **plain `UnityFS`**, not
    AES-header-encrypted. UnityPy loads `defines/common` (875 objects),
    `chara/chara_001` (11,658 objects) and `common_battle` directly.
- **SBR2 update** uses delta fragments; its RomFS needs a delta-aware extractor
  (hactool/Ryujinx dump). The base alone is already moddable.
- Ryujinx (Ryubing 1.3.3 Flatpak) is installed and has keys/firmware paths, so
  it is the practical test/install/dump environment.

## Feasibility matrix

| Goal | Feasible? | Why |
|---|---|---|
| **Switch asset/data mods (SBR1)** | **High** | Same Unity 5.6 bundles and RomFS paths as PC. LayeredFS replaces individual bundles. |
| PC data/asset mods → Switch (SBR1) | High | Edit the bundle with UnityPy, repack, place in LayeredFS. |
| Switch platform-exclusive Max/Caesar → PC | **Already available** | The `N` platform pack is **MAX** (`model_pc381_max`) and **Caesar** (`model_pc431_ceaser`), both present in the PC chara packs and already unlockable via the platform-exclusive toggle. No Mario/Luigi/Wario content exists in the data. |
| Switch maps/stages → PC | Medium | Map/env/thumbnail bundles may be compatible; table IDs and scene flow need checks. |
| **Full QoL plugin/F9 menu on Switch** | **Low** | Switch SBR1 is ARM64 IL2CPP (`Data/Managed/Metadata`), not a .NET `Assembly-CSharp.dll`. BepInEx 5 x86/x64 cannot run. Requires ARM64/IL2CPP hooks or a sysmodule. |
| Simple cheat toggles on Switch | Medium | Atmosphere cheats can patch memory/values, but not complex menus, randomizers or save-scoped logic. |
| **SBR2 asset/data mods (Switch base)** | **High** | Base RomFS extracted; Switch bundles are plain Unity 2020.3.36f1 and load with UnityPy. |
| SBR2 PC ↔ Switch data/assets | **High for base content** | Same Unity version and table schemas; Switch bundles are easier (no AES header). Update-only content needs delta extraction. |
| SBR2 code mods | Low | IL2CPP ARM64; no BepInEx on Switch. |
| Repack modified NSP | **Not recommended** | Requires NCA re-encryption/signing/tickets; LayeredFS is the standard, safer path. |

## Recommended approach

### 1. SBR1 Switch asset mod pack (LayeredFS)

Build a zip containing:

```text
atmosphere/contents/01007AD00013E800/romfs/Data/StreamingAssets/InAppAssets/...
```

(or the base title ID `01007AD00013E000` for files that exist only in base).

Candidate mods that need no code:

- Character palette skins (bake the texture into `chara_pack_01`).
- BGM/SFX replacement (`sound/bgm` bundles).
- Locale/text edits.
- Intro/movie replacement, if the Switch movie bundles are editable.
- Data-table tweaks in `common_pack` (EXP, shop, pickup tables) if they do not
  depend on plugin code.
- Map changes.

### 2. Port Switch-exclusive content to PC

There are **no Nintendo-owned characters** (Mario, Luigi, Wario, Donkey Kong,
etc.) in the SBR1/SBR2 data inspected so far. The Switch platform pack `N` is:

- `N001` — **MAX** (`model_pc381_max`, mesh `mesh_pc381_max`)
- `N002` — **Caesar** (`model_pc431_ceaser`)

Both models are present in the PC chara packs (`chara_pack_03` and
`chara_pack_04`) and are already exposed by the platform-exclusive toggle. The
Switch update therefore does not unlock a hidden Nintendo roster.

Switch-exclusive work is limited to maps, costumes, music, UI or data tables
that are not in the PC build; verify those after delta extraction.

### 3. SBR2 (now unblocked for base content)

- Base RomFS is extracted and loads with UnityPy; no AES decryption is needed
  for Switch bundles.
- Start with data/asset patches in `defines/common`, `chara`, `costume`, `map`
  and `sounds`; build a LayeredFS pack under the SBR2 base/update title IDs.
- Use `tools/sbr2_data_patch.py`/`sbr2_pack.py` as the basis, with the
  decryption step bypassed for Switch bundles.
- Extract the update with a delta-aware tool for update-only content.

### 4. Code features

Do **not** promise F9/randomizer/practice on Switch. They need ARM64 IL2CPP
instrumentation. Realistic alternatives:

- Atmosphere cheats for simple values (stats, lives, timers).
- A native ARM64 patch for small, well-defined changes.
- Keep the full launcher/plugin on PC.

## Feature portability matrix

Switch has no BepInEx. Anything that runs in the PC plugin's C# code needs an
ARM64/IL2CPP reimplementation. Asset and data-table features are portable via
LayeredFS.

### Portable (asset/data only)

| Launcher/plugin feature | How it ports |
|---|---|
| Character palette skins | Replace `Texture2D` in `chara_pack_*` / `chara` bundles |
| Custom character models | Bake a rigged mesh/material into a chara bundle (no runtime auto-weights) |
| Custom BGM / SFX | Replace audio clips in `sound/bgm` / `sounds` bundles |
| Intro skip | Replace the intro movie asset with a blank clip |
| Publisher logo removal | Replace logo textures/UI assets where state removal is not required |
| Language / Japanese voices | Edit save language byte or use the game's language menu; voice packs are already on the cart |
| Max/Caesar unlock | Data/progression edit if not already unlocked by the update |
| Master difficulty data | Edit difficulty/story tables; menu toggle may still be code-gated |
| EXP / shop / story balance | Edit `StoryCampExpTable_*`, shop and story tables |
| Static map remixes | Replace `map/data` bundles |
| Pickup/item table edits | Edit `defines/common` data tables (fixed layout, not random) |
| Player icons / topknots | Replace icon/acce assets or edit unlock tables |
| Post-FX removal | Edit the post-processing profile assets (static, not a runtime toggle) |
| Save backups | External homebrew (JKSV/Checkpoint), not an NSP mod |
| Custom skin/asset cross-port | Copy bundles between PC and Switch where the Unity version/format matches |

### Possible with save editing or Atmosphere cheats

| Feature | Limit |
|---|---|
| Unlock characters/topknots/cassettes | Save flags, not the PC plugin's live menu |
| Gems / currency / story progress | Save edits |
| Max stats / lives / timer | Atmosphere cheats; no persistent menu |
| Stage jump / practice-like setup | Cheat/save hack; no F9 practice UI |
| Random run | Not realistically; could pre-bake a fixed sequence |

### Not portable without code work

| Feature | Why |
|---|---|
| F9 overlay / Settings menu | C# IMGUI + plugin lifecycle |
| Practice mode, stage select, repeat | Scene-flow code |
| Random Run | `StoryUtility.initToNext` + state machine |
| Random characters / enemies / pickups per run | Runtime hooks and RNG |
| Skip character select / gem continue | UI/scene code |
| Per-stage/Master stat reset | Player status code |
| CPU A-button specials / smarter bots | NPC AI code |
| Faster result screens / soft reset | Timing and scene code |
| Borderless / aspect ratio | PC launch flags; Switch uses native modes |
| Launcher install/config/art/audio tools | PC-only launcher |
| Online-room achievement helper | PC/Steam-specific |

**Recommendation:** treat the Switch release as an **asset/data mod pack**
(skins, audio, movies, maps, balance, unlocks), and keep the full F9
randomizer/practice suite on PC. SBR2 follows the same rule, with plain
UnityFS bundles making asset edits easier but no code plugin support.

## Legal/distribution

- Never commit NSPs, keys, or extracted game assets.
- Ship **patch-only** LayeredFS packs; users supply their own NSP and keys.
- Offline/private use only; no online cheating or modified online play.

## Effort

| Item | Estimate |
|---|---|
| Install/inspect SBR1 base+update in Ryujinx, dump update RomFS | 0.5–1 day |
| SBR1 LayeredFS asset mod pack + installer | 2–4 days |
| Switch-exclusive character extraction → PC | 1–2 weeks |
| SBR2 base/update extraction and format comparison | 2–5 days after base is available |
| ARM64/IL2CPP code mods | weeks–months; separate project |

## Decisions needed

1. Get a valid **SBR2 base NSP** (the current RAR is 0 bytes)?
2. Start with **SBR1 asset mods**, **Switch→PC character port**, or both?
3. Distribution format: LayeredFS zip only (recommended) or attempt NSP repack?
4. Test target: Ryujinx only, or real Switch hardware?
5. Is code-level Switch support in scope at all, or asset/data mods only?
6. Which SBR1 update content is highest priority: maps, music, costumes or
   UI/text? (There is no Nintendo-owned character roster to port.)
