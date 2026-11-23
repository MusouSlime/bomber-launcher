# Scope: characters, skins, arenas, and the rest

Status: planning document. No feature in this file is implemented yet except
where marked **done**.

## Data inventory

Verified from `InAppAssets/steam/common_pack.bin` (Unity 5.6 asset bundle) and
the installed Steam files.

| Item | Count | Notes |
|---|---:|---|
| Builtin character rows | 51 | includes the 8 base colors, story/shop characters, and 10 platform IDs |
| DLC/AOC character rows | 10 | Tako, Golden Vic Viper, and `*_p` variants of the 8 base colors |
| Platform-exclusive IDs | 10 | M001–M003, V001–V002, S001–S003, N001–N002 (**done**) |
| Cassette skins | 32 | status IDs 10010–10221; base 8 colors use cassettes |
| Topknots (hats) | 305 builtin + 1 DLC placeholder | cosmetic; `acce_cat_*` packs present |
| Character status rows | 105 | combat stats, skills, cooldowns, icons |
| NPC AI rows | 18 per table (`normal`/`strong`/`weak`) | only the 18 base characters |
| Grand Prix AI rows | 32 per rule | team/leader AI for battle rules |
| Multiplayer maps | 60 builtin + 0 DLC | `map/data/map_data_pack_vs4_*`, env + thumbnail packs present |
| Story maps | 62 (worlds 1–5 and 7 have 10 each; world 6 has 2) | plus 13 boss encounters |
| Character packs on disk | `chara_pack_01`–`04` | all four present |
| Accessory packs on disk | `acce/acce_cat_001`… | present |

## 1. More characters and skins

### 1A. Unlock all selectable characters

**What exists:** 51 builtin + 10 DLC rows. Ten platform IDs are already enabled.
The remaining rows are gated by `CharacterDataManager.isUsableCharacterInternal`
/ `isUsableBuiltinCharacter` and the save-reference keys.

**Approach**

- Generalize `PlatformCharacters` into `CharacterUnlocks` with sets:
  - builtin story/shop characters
  - DLC/AOC characters (`contentType = 1`, `chara_pack_02/03`)
  - platform IDs (already working)
- Patch the same two methods plus `isBuyedCharacter` for the enabled sets.
- Inject missing locale names into `LocaleDef.LOCALE_TABLE` and
  `LocaleDataTable` (same mechanism as platform names).
- Keep `golem` (`refAlwaysUnusable`) and obvious NPC-only rows excluded.

**Risks**

- Some rows may have no player model, voice, or result icon in the Steam build.
- DLC rows may also be checked by AOC ownership paths.
- Platform/DLC rows cannot be CPU opponents (no `CharacterNpcAiInfoData`).
- Some story characters may have broken status IDs or missing skill assets.

**Effort:** 1–2 days to enable + 1–2 days to test every row.

### 1B. Cassette skins

**What exists:** 32 skins with status IDs 10010–10221. The 8 base colors have
`_useCassette = 1`; the game already uses cassettes for their alternate looks.

**Approach**

- Patch `isUsableCassette` and `isUsableBuiltinCharacter` for cassette save
  references.
- Verify each cassette's status ID, icon, voice, and model pack.
- Add locale names only where the table lacks one (the `CASSETTE_*` keys
  already exist in the base locale table).

**Risks:** some cassettes may share a model with a status variant that expects
a different skill asset; check the character-select preview and battle model.

**Effort:** 2–3 days including a full visual test pass.

### 1C. Topknots

**What exists:** 305 builtin hats, accessory packs on disk, locale names.

**Approach**

- Patch `isUsableTopknot` / `isBuyedTopknot` (and the saved-reference helpers)
  behind a separate toggle.
- Optionally add a curated “all hats” toggle instead of exposing 305 rows at
  once.

**Risks:** UI clutter/slow customization list; no gameplay impact.

**Effort:** 1–2 days.

### 1D. Character/skin selector in the QoL menu

Not required if the in-game character select shows the unlocked rows. A simple
launcher panel could list counts and toggle categories. Defer.

### 1E. CPU versions of unlocked characters

- Option 1: keep them player-only (current platform behavior).
- Option 2: reuse a base AI profile by category (e.g., white/black) for CPU
  slots. Needs testing of animations, revenge carts, and skills.
- Option 3: use the 32 Grand Prix AI rows where a matching key exists.

**Recommendation:** ship 1A–1C player-only first; add 1E as an experimental
toggle later.

## 2. Multiplayer stages in Random Run

### What exists

- 60 multiplayer maps with map/env/thumbnail/BGM paths.
- No story flow entry, no enemy start points, no annihilate goal.
- Story battles expect a `StoryMapInfoData` row (`_goalType`, map pack, env,
  boss info) and a `StoryFlowData` battle event.

### Approach A — Arena intermission (recommended first)

- Every N random-run stages, insert one arena from the 60 multiplayer maps.
- Build a synthetic `StoryMapInfoData` clone per arena:
  - `mapPackName` / `mapFileName` from the multiplayer row
  - `envPackName` / `envFileName` from the multiplayer row
  - `_goalType = 0` (annihilate), no boss
  - reuse a story camera param or `multi_cmn`
- Serve the synthetic row through a patched `StoryMapInfoDataTable` lookup.
- At `AbstractBattleSceneBase.endLoadMapData`, generate enemy spawn points from
  valid empty tiles near the player starts, set `useEnemyList` from a pool whose
  packs are already loadable, and let `EnemyController` spawn them.
- Clear condition: all injected enemies dead; no boss.
- Give the arena its own result/reward handling or treat it as a normal stage.

### Approach B — Separate Arena Run

A dedicated run mode: 10 random multiplayer arenas, each with a fixed enemy
budget and a defeat-all goal. Simpler to reason about than mixing into the
story flow, but needs its own scene-flow plumbing.

### Approach C — Mixed pool

Replace a percentage of normal random-run stages with arenas. Most flexible,
but hardest to balance and to guarantee boss pacing.

**Risks**

- Spawn-point validity and pathfinding on PvP layouts.
- Multiplayer maps may lack breakable blocks/items needed for a fun PvE stage.
- Story clear/result UI assumptions (enemy counters, boss slots).
- Camera parameters and map bounds.
- Enemy packs must be loaded before spawn; use only names in a known pool or
  extend `useEnemyList` before `entryLoadUnit`.

**Effort:** 3–5 days for a prototype of Approach A; 1–2 weeks for a robust,
balanced mode with rewards and UI.

## 3. Other gaps and candidates

### Random Run / Randomizer

- **Move-set randomizer** — shelved. Needs skill-asset/icon alignment and a
  compatibility filter; current dormant code only writes status slot values.
- **Enemy randomizer v2** — global enemy pool, strong/weak variants, per-stage
  budgets, no repeats, boss minions.
- **Seeded runs** — expose the existing seed and show it on the run summary.
- **Stage filters** — exclude water/ice/maze layouts, choose biomes, boss-only
  rush.
- **Modifiers** — low gravity, no pickups, one-life, double enemies, mirror
  controls. Each needs explicit plugin hooks.
- **Run summary / history** — stages cleared, bosses defeated, seed, time.

### Multiplayer

- Local Grand Prix rules: crystal, checkpoint, escort, classic.
- Team selection and CPU teammates.
- CPU AI for DLC/platform characters (see 1E).
- Arena stage selection and custom rules in the launcher/QoL.

### Launcher / platform

- **Windows artwork extraction** (Linux has it; Windows currently expects a
  manually copied `assets/` folder).
- Auto-update check against GitHub releases.
- Save profile manager: named backups, import/export, restore.
- Flatpak / Steam Deck packaging.
- Localization of injected character/cassette names (currently English only).
- Crash/log viewer and config validation.
- Apply BepInEx config changes without a full game restart where possible.

### In-game QoL

- 4K/ultrawide menu scaling (the overlay is now full-screen; fonts are still
  IMGUI defaults).
- Per-tab controller focus memory.
- Practice loadout presets (multiple named loadouts).
- Quick “restart current battle” and “skip to boss” helpers.
- Character/skin gallery with locked/unlocked counts.

### Constraints to keep

- No stat injection, auto-wins, or fake counters.
- Achievements remain naturally earned; only the dead online-room helper.
- B = normal bomb, A = special.
- Practice defaults to default stats and never randomizes characters unless the
  player opts in.
- Platform exclusives remain player-selectable only unless an AI fallback is
  explicitly enabled.

## Recommended sequence

1. **0.7 — Character & skin unlock** (1A + 1B, optional 1C): highest value,
   lowest risk, reuses the platform-ID machinery.
2. **0.7.x — Test pass + localization** for every unlocked row, plus a Windows
   artwork extraction parity item.
3. **0.8 — Arena prototype** (Approach A): one arena intermission every N
   stages, fixed enemy pool, defeat-all goal.
4. **0.8.x — Arena polish**: spawn placement, rewards, camera, map filters,
   balance.
5. **0.9 — Random Run 2.0**: seeds, stage filters, modifiers, run summary.
6. **1.0 — Move sets / enemy v2 / platform polish** once the core run is stable.

## Decisions needed

1. Character scope: builtin only, builtin + DLC, or everything including
   cassettes and topknots? Exclude `golem`/NPC-only rows?
2. Cassettes: expose all 32 as selectable skins, or curate a subset?
3. Topknots: unlock all 305, or a smaller curated set?
4. Arena integration: intermission (recommended), separate Arena Run, or mixed
   stage pool?
5. CPU use for DLC/platform characters: player-only (current) or reuse base AI?
6. Priority: characters first, arenas first, or both in one release?
