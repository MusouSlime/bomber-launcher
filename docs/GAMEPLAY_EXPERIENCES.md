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

## Practice stage select

Plugin 0.3.6 adds an in-game **F9** overlay for practicing any story stage.
Choose world 1–7, stage 1–10 and Easy/Normal/Hard/Master, then **Start
Practice**.

- **F9** or **L1 + R1 + X** (hold 0.6 s) opens/closes the picker.
- Arrow keys or D-pad move between World, Stage, Difficulty and Stats and
  change the selected value.
- **Enter / A** starts practice; **B / Esc** closes.
- **Stats** row: `Save` uses the current save's item counts, `Default` clears
  all item bonuses, `Max` gives fire 5 / bomb 5 / speed 4 plus punch, glove,
  kick, remote, penetration and elasticity, and `Custom` reads the
  `StatsFire`, `StatsBomb`, `StatsSpeed`, `StatsPunch`, `StatsGlove`,
  `StatsKick`, `StatsRemote`, `StatsPenetration`, `StatsElasticity` config
  values.
- Mouse clicks also work; the game's own menu input is blocked while the
  picker is open.
- Looks up the `StoryFlowDataTable` entry for the requested stage.
- Sets `StoryManager` to the flow just before that battle.
- Sets `MoveSceneParam.storyParam` to the target stage and loads `StageSetup`,
  so character selection still works normally.
- Sets the difficulty and player stock before the setup scene.
- `Practice.Active` suppresses `SaveDataManager.startStorySave` and
  `startSaveAll`, so practice does not overwrite the real story save.

`[Practice] PracticeMenuKey = F9` changes the keyboard key;
`PracticeMenuPadCombo = 4,5,2` and `PracticeMenuPadHold = 0.6` control the
controller combo (4=L1, 5=R1, 2=X). Select is not exposed by the game, so it
cannot be used here.

## Soft reset

The retail game has a `Reset` scene and `ResetUtility.resetStart()` which
resets every singleton and reloads `Entry` — an in-process reset to title. The
plugin’s combo now defaults to a **full process relaunch** instead:
`SoftResetRelaunch = true` writes a small delayed `bomber_relaunch.bat`, starts
the game executable again, and quits the current process. Set it to `false`
to use the old in-process title reset.

- **Hold LB + RB + Start (L1 + R1 + Options) for 0.75 s**
- `SoftResetEnabled` (default `true`)
- `SoftResetPadCombo = 4,5,7` — button indices: `0=A/Cross, 1=B/Circle,
  2=X/Square, 3=Y/Triangle, 4=LB/L1, 5=RB/R1, 6=Back/Share, 7=Start/Options`
- `SoftResetPadHold = 0.75` seconds (0.2–3.0)
- `SoftResetRelaunch = true` (default) restarts the process.
- `SoftResetSave = false` only applies to the non-relaunch reset path.
  Saving a story battle can write stock 0 and leave the save unloadable.
- Keyboard shortcut active by default: `SoftResetKeyEnabled = true`,
  `SoftResetKey = F10`, `SoftResetRequireCtrl = true` → `Ctrl+F10`

The game also has two narrower resets already in the UI: **Retry / Return to
World Map / Main Menu** in the battle pause menu, and **Restart Story** on the
save-select screen (starts story over while keeping system unlocks and gems).

## 2. Hard modes — easy/medium

The selector only names a difficulty; the difficulty is expressed elsewhere:

- `StoryInfoDataTable_easy/normal/hard/super` — stock and economy:

| Difficulty | Stock | Stock gem | Continue gem | No-continue gem |
| --- | --- | --- | --- | --- |
| easy | 9 | 20 | 10 | 300 |
| normal | 7 | 50 | 300 | 800 |
| hard | 5 | 70 | 600 | 1000 |
| super | 3 | 100 | 800 | 1200 |

- Four `EnemyDataTable` variants change enemy move speeds
  (average 0.91 / 2.20 / 2.86 / 3.30 for easy / normal / hard / super).
- `BattleDefData` holds gravity, fall speed, ready timers, block placement
  rate, result timings.
- `BossNPCDataTable` selects boss AI packs.

### Master (`super`) table audit

All per-difficulty data referenced by `DifficultyData.fileNameSuffix ==
"_super"` exists in the retail packs:

| Data | Super variant | Status |
| --- | --- | --- |
| `DifficultyDataTable` | `id=3`, `_fileNameSuffix="_super"` | present |
| `StoryInfoDataTable_super` | 7 worlds; stock 3, 1200/2300 gems | present |
| `EnemyDataTable_super` | 30 enemies, speeds 1.5–4.5 | present |
| `BossRobotData` | `br001…br008 *_super` | present |
| `BossNPCEquipmentItemDataTable_super` | all boss characters | present |
| `BossNPCDataTable` AI files | `BossNpcGolem/Phantom/Plasma/Sexy/TestAi_super` | present |

`WorldMapScene.loadDifficultyWorldData` sizes its array from
`DifficultyDataTable.difficultyDataCount` (4), and each entry has 32 star
slots, so Master progress/star display is supported. `StoryData.difficulty`
stores `DifficultyData.id` directly (3) with no clamp, and loading a save uses
`getDifficultyData(3)`. No stage/map layout is difficulty-suffixed; layouts are
shared and only story economy, enemy stats, boss stats/AI and boss equipment
change per difficulty.

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

## Custom BGM

Choose a folder in either launcher or set:

```ini
[Audio]
CustomBgmFolder = /path/to/music
CustomBgmShuffle = true
CustomBgmVolume = 0.8
CustomSfxFolder = /path/to/sfx
CustomSfxShuffle = true
CustomSfxVolume = 0.8
```

Supported files: `.ogg`, `.wav`, `.mp3`. The plugin hooks
`SoundManager.playAsBGM` / `changeBGM` and plays matching files through a
persistent `AudioSource`:

- Exact label match: `BGM_sys_title_01.ogg`, `snd_bgm_btl_stg_01.wav`, etc.
  The label is the game’s BGM name without extension.
- `CustomBgmShuffle = true` picks a random unmatched `BGM_*` / `snd_bgm_*`
  track; exact matches still win.
- `CustomSfxFolder` replaces sound effects. Name files `<label>.ogg` or
  variants `<label>_1.ogg`, `<label>_2.ogg`; `CustomSfxShuffle` picks a random
  variant.
- Jingles (`SE_jingle_*`) are left to the original audio unless matched.
- `stopBGM`, pause/resume and restart are forwarded to the custom source.
- Custom SFX IDs are tracked so `SoundManager.stop`, `isPlaying` and `stopAll`
  still work for replaced sounds.
- The path is converted from `/home/...` to `Z:\home\...` automatically under
  Proton.

The folder picker is in **Audio → Custom BGM Folder** in both launchers.

## Randomizer / roguelike base

Plugin 0.5.3 adds two independent switches under `[Randomizer]`:

```ini
[Randomizer]
RandomStoryCharacters = false
RandomMoveSets = false
Seed = 0
SkipCharacterSelect = true
```

- **RandomStoryCharacters** — a prefix on `StageSetupUI.init` picks random
  entries from `CharacterDataManager.getCharacterInfoFromSorted()` and writes
  their `CharacterID` into `StoryData.characterDatas` before
  `setFromSaveDataToBattleParams` builds `MoveSceneParam`. P1/P2 are distinct.
- **RandomMoveSets** — once per launch, walks
  `CharacterStatusDataTable` and assigns random `Player.SKILL_TYPE` values to
  `_normalSkillType` and `_specialSkillType`. Skill assets/icons stay at their
  original paths, so visuals may not match the rolled ability.
- **Seed** — a non-zero value makes both rolls reproducible; 0 uses
  `Environment.TickCount`.
- **SkipCharacterSelect** — when random characters (or a random run) are
  active, `StageSetupUI` auto-calls `moveNextScene(1)` after 0.25 s instead of
  showing character select. Set false to keep the screen.
- **Random 10 + Boss** — in the F9 practice picker, select `Run: 10 + Boss`.
  It starts at a random non-boss stage, then after each battle sets the next
  story flow to another random non-boss stage. After 10 stages it selects a
  random stage 10 boss. `StoryUtility.initToNext` is only overridden when the
  previous scene is `StoryBattle`, so StageSetup/WorldMap transitions still
  behave normally.

Both are exposed as launcher toggles. Practice saves remain suppressed, so this
is a safe base for a roguelike run: random characters, random move sets,
practice stage jumps and no real-save writes.

## Dead online room achievements

`Room Creator`, `Room Master` and `Room Champ` are driven by
`TrophyManager.FuncCreateRoom`, which is only reached from the online
`InternetReady` / `GPInternetReady` scenes. The launcher can queue a one-shot
helper:

```ini
[Achievements]
UnlockRoomAchievements = true
```

On the next launch the plugin calls `FuncCreateRoom` 100 times, which updates
the same `RoomCreateCount` stat and unlocks all three achievements. This is an
explicit shortcut for dead online content, not a real room or match.

## Display aspect ratios

The launcher’s **Aspect Ratio** selector supports Native, 16:9, 16:10, 21:9
and 4:3. It computes the largest window with that ratio that fits the current
desktop, centers it with `-screen-x/-screen-y`, and launches with
`-popupwindow -screen-width W -screen-height H`. On a 1600×1200 desktop this
gives 1600×900 for 16:9, 1600×1000 for 16:10, 1600×684 for 21:9, and
1600×1200 for 4:3.

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
