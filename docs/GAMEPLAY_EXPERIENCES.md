# Extra gameplay experiences — feasibility

Everything below is based on the shipped data tables, decrypted from
`StreamingAssets/InAppAssets/steam`. Pack edits are reversible; Steam
"Verify integrity" restores vanilla files.

## Where the gameplay data lives

| Pack | Contents |
| --- | --- |
| `common_pack` | `CharacterInfoDataTable_builtin` (51 chars), `_dlc` (10), `CharacterStatusDataTable` (105 statuses), `DifficultyDataTable` (easy/normal/hard/super), `StoryInfoDataTable_{easy,normal,hard,super}`, `StoryMapInfoDataTable_world1..7` (10 stages each), `StoryFlowDataTable` (17 events), `StoryRewardDataTable`, `GPMapInfoDataTable` (10), `MovieDataTable`, `EquipmentInfoDataTable` |
| `common_battle_pack` | `ItemDataTable` (15 items), four `EnemyDataTable` variants (30 enemies each), `GimmickDataTable`, `BlockCommonDataTable` |
| `define` | `PlayerDefData`, `BombDefData`, 28 `PlayerSkill*DefData`, `GimmickDefData`, `BattleDefData`, `MultiBattleDefData` |
| `define_boss_npc_pack` | `BossNPCDataTable` (5 bosses) + equipment |
| `chara/chara_pack_01..04` | character prefabs: `PlayerComponent`, `MeshCustomizer`, `SkinnedMeshCustomComponent`, `Animator`, meshes/avatars |

## 1. Move set swapping — easy, best first pick

- `CharacterStatusDataTable` entries carry `_normalSkillType` and
  `_specialSkillType`, both using `Player.SKILL_TYPE` (0–28):
  `NORMAL_BOMB=1`, `MAGNET_BOMB=2`, `GOLEM_BOMB=3`, `PHANTOM_BOMB=4`,
  `SEXY_BOMB=5`, `PLASMA_BOMB=6`, `ATTRACT=7`, `SPEED_UP=8`, `KILLER=9`,
  `TRACE=10`, `SHIELD=11`, `TEMPTATION_FIELD=12`, `WARP=13`, `SWAP_WARP=14`,
  `CONTINUOUS_BOMB=15`, `BOMB_SLIDE_MINE=16`, `GRANT_FIRE_TOLERANCE=17`,
  `SOURCE_OF_SICK=18`, `BOMB_EXPLOSION_SHOT=19`, `BOMB_INVALIDATE_SHOT=20`,
  `GRANT_ACTION_ENHANCEMENT=21`, `GALLOPING_ATTACK=22`,
  `OBLIQUE_CONTINUOUS_BOMB=23`, `INVISIBLE=24`, `TO_BE_MIST=25`,
  `GRANT_SHORTEN_EXPLOSION=26`, `GRANT_LENGTHEN_EXPLOSION=27`,
  `GRANT_BUILD_UP=28`.
- `CharacterInfoDataTable` maps each character to a `_normalStatusId` and
  `_uniqueStatusId`. Example: Red/White/Black/Blue all use normal status `1`;
  Red's unique status is `10004`, White `10001`.
- Swapping is therefore a one- or two-field data edit per character, or a
  runtime shuffle at battle load. `_skillAssetsPath` / `_skillIconPath` only
  affect visuals; the enum drives the actual behaviour.

## Hidden Master difficulty

`level8` (the world map scene) contains a **`SuperButton`** under
`DifficultyCanvas`, but the GameObject is inactive. The difficulty `Dialog`
already has a four-entry option array: `EasyButton`, `NormalButton`,
`HardButton`, `SuperButton`, and the button's click handler is wired to
`closeDialog`. There is no unlock condition in the code — the button is simply
hidden.

Plugin config `[Gameplay] UnlockMaster = true` (launcher: **Master
Difficulty**) activates the button at scene load. The vanilla Login/Logout
navigation loop did not include it (Hard's Down pointed back to Easy), so the
plugin also sets explicit Up/Down links and re-wires the click to
`WorldMapUI.closeDialog(3)`.

## Debug menu

`StreamingAssets/.../debug/debug_menu_pack` contains a complete `DebugCanvas`
(bool/int/float fields, buttons, scroll view), but its scripts (`DebugMenuBool`,
`DebugCanvas`, ...) are **not present in any shipped DLL**. It is dead content
from a development build; there is no end-user debug menu or command-line flag.
The Master button mod is the only retail access route besides save editing.

## Launch switch

The game has **no command-line parsing** (no `GetCommandLineArgs`, no custom
flags), so there is no native argument for difficulty or the debug menu. The
plugin adds one:

```
BOMBER_FORCE_MASTER=1 %command%
```

Set that in Steam launch options on Linux. When the world map loads, the plugin
calls `StoryManager.setCurrentDifficulty(findDifficulty("super"))` and logs
`Forced Master difficulty via BOMBER_FORCE_MASTER`. On Windows, set the same
environment variable in a shortcut/batch that calls `steam -applaunch 702700`,
or use the launcher toggle.

## 2. Hard modes — easy/medium

The selector only names a difficulty; the difficulty is expressed elsewhere:

- `StoryInfoDataTable_easy/normal/hard/super` — stock and economy:

| Difficulty | Stock | Stock gem | Continue gem | No-continue gem |
| --- | --- | --- | --- | --- |
| easy | 9 | 20 | 10 | 300 |
| normal | 7 | 50 | 300 | 800 |
| hard | 5 | 70 | 600 | 1000 |
| super | 3 | 100 | 800 | 1200 |

- Four `EnemyDataTable` variants change enemy move speeds (roughly
  0.8 / 2.0 / 2.6 / 3.0).
- `BattleDefData` holds gravity, fall speed, ready timers, block placement
  rate, result timings.
- `BossNPCDataTable` selects boss AI packs.

**Design:** a "Nightmare" plugin mode that sets stock to 1, disables
continues, scales enemy life/speed and boss AI, optionally halves the battle
timer. Runtime plugin is safer than pack edits and can be toggled.

## 3. Challenge modes — medium

- `BattleDefData`: round timers, gravity, fall speed, pressure, block
  placement rate (`_randomBlockPlacementRate`), result delays.
- `MultiBattleDefData`: gem participation bonuses.
- `ItemDataTable` weights: fire-up 12, bomb-up 12, speed-up 12, punch 4,
  power glove 5, kick 5; several items are weight 0 and unused.
- `GPMapInfoDataTable` rules: crystal, classic, escort, checkpoints.

**Design:** launcher toggles backed by plugin config:
- No items / only movement items / random item table
- 1 stock, no continues
- 60-second sudden death
- Low gravity / high gravity
- Random map + random rule

No pack edits required; this can be a pure BepInEx feature.

## 4. Randomizer — medium

What can be randomized today:

- **Items:** shuffle `ItemDataTable._appearanceRatioWeight` (15 entries).
- **Enemies:** shuffle names/stats in the four `EnemyDataTable` variants
  (30 entries each).
- **Skills:** shuffle `_specialSkillType` across status entries.
- **Maps/bosses:** each `StoryMapInfoDataTable_worldN` has 10 stages with
  `_mapPackName`, `_mapFileName`, `_appearBossType`, `_bossPackName`,
  `_bossFileName` — swap stage maps and bosses between slots.
- **Story order:** `StoryFlowDataTable` (17 events) controls world/stage/movie
  flow.
- **Block layouts:** maps are Unity scenes; true per-block randomization is
  harder. `BattleDefData._randomBlockPlacementRate` is the only exposed knob.

**Design:** an offline "seed" tool that decrypts the packs, shuffles chosen
tables, re-encrypts with the correct filename key, and writes a backup +
restore script. A runtime plugin could do the same after load, but is harder
to make deterministic.

## 5. Importing models — hard, do last

- **Easy:** texture/costume reskins. Textures are already exported by
  `tools/extract_ui_assets.py`; replacing a texture in a chara bundle is the
  same pipeline.
- **Medium:** swap existing character prefabs by editing `_charPack` /
  `_charPrefab` in `CharacterInfoDataTable`. The rig/animator must match.
- **Hard:** custom meshes. A player prefab expects `PlayerComponent`,
  `MeshCustomizer`, `SkinnedMeshCustomComponent`, `Animator` and a matching
  skeleton. Standard Bomber bodies use 29 bones; some characters use 72.
  Custom meshes must be skinned to that skeleton, and animations/colliders must
  line up. This needs Unity 5.6 asset-bundle tooling (or careful UABEA/UnityPy
  mesh injection), not just a file swap.

## Recommended order

1. **Move set swapper/randomizer** — smallest change, immediate gameplay
   variety, low risk.
2. **Challenge/hard-mode plugin** — no pack edits, toggles in the launcher.
3. **Item/enemy/map randomizer** — offline seed tool with backups.
4. **Costume/texture swaps** — reuse the existing art pipeline.
5. **Custom model import** — separate asset-tooling project.

## Risks

- Every pack edit must preserve the filename-derived AES key; test decrypt →
  edit → re-encrypt → decrypt.
- Stage/map swaps can break story progression or saves; always keep a save
  backup.
- Do not use modified data in online modes or leaderboards.
- Runtime plugins are preferable for toggles; offline pack edits are better for
  full randomizer seeds.
