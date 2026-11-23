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
| Switch-exclusive Nintendo characters → PC | **High, pending update extraction** | Switch update should contain the Nintendo packs; PC Steam does not. Same engine/format. |
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

This is the most valuable cross-platform direction:

1. Extract the SBR1 2.2 update with a delta-aware tool.
2. Inspect `chara_pack_02/03/04` and `CharacterInfoDataTable_*` for
   Nintendo-exclusive entries.
3. If the bundles load with UnityPy, extract models, textures, icons, voices and
   status rows.
4. Add them to the PC `common_pack`/`chara_pack` bundles and extend the existing
   platform-character unlock (or a new "port" toggle).
5. Test in the PC build; keep PC and Switch mods separate.

The same route can bring other Switch-only maps, costumes or music to PC.

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
6. Which SBR1 update content is highest priority: Nintendo characters, maps,
   music, or UI/text?
