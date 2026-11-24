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
CustomBgmFolder = /path/to/ogg-cache
CustomBgmSource = /path/to/music
CustomBgmShuffle = true
CustomBgmVolume = 0.8
CustomSfxFolder = /path/to/sfx
CustomSfxShuffle = true
CustomSfxVolume = 0.8
```

Unity's `WWW` loader in this build only decodes OGG/WAV reliably, so the
launcher converts the chosen music folder into a flat OGG cache with ffmpeg
(MP3, FLAC, M4A, AAC, OPUS and WMA) and copies `.ogg`/`.wav` unchanged.
Conversion runs in parallel and remembers the original folder in
`CustomBgmSource`; **Clear** deletes the cache. Folder scanning is recursive,
so album subfolders work. If the plugin is pointed directly at a folder that
only has MP3s it logs a skip warning instead of failing every track.

The plugin hooks `SoundManager.playAsBGM` / `changeBGM` and plays matching
files through a persistent `AudioSource`:

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

The folder picker is in **Audio → Custom BGM Folder** in both launchers. It
must be used while the game is closed; the resulting cache lives under the
launcher's data folder (next to a portable build, or under
`~/.local/share/bomber-launcher`). Restart the game after converting.

## Randomizer / roguelike base

Plugin 0.5.3 adds two independent switches under `[Randomizer]`:

```ini
[Randomizer]
RandomStoryCharacters = false
RandomEnemies = false
Seed = 0
SkipCharacterSelect = true
RandomPickups = true
RandomPickupCount = 10
RandomPickupPool = Difficulty
HidePickupIcons = true
PickupToast = true
ResetStatsPerStage = true
```

- **RandomStoryCharacters** — a prefix on `StageSetupUI.init` picks random
  entries from `CharacterDataManager.getCharacterInfoFromSorted()` and writes
  their `CharacterID` into `StoryData.characterDatas` before
  `setFromSaveDataToBattleParams` builds `MoveSceneParam`. P1/P2 are distinct.
- **RandomEnemies** — after `AbstractBattleSceneBase.endLoadMapData`, each
  `RuntimeMapData.enemyStartPointDatas[i].name` is replaced with a random entry
  from the same map's `useEnemyList`. The map's enemy models/SE packs were
  already queued for loading, so this only changes which regular enemy type
  appears at each spawn point. Bosses are separate and unaffected. Applies to
  `StoryBattle` (including practice). The list is deduplicated and re-rolled
  again immediately before `EnemyController.create`, so the spawner cannot
  read stale names. Logs show the unique pool and the actual spawn counts; a
  map with only one listed enemy type logs that nothing can vary.
- **Seed** — a non-zero value makes both rolls reproducible; 0 uses
  `Environment.TickCount`.
- **SkipCharacterSelect** — when random characters (or a random run) are
  active, `StageSetupUI` auto-calls `moveNextScene(1)` after 0.25 s instead of
  showing character select. Set false to keep the screen.
- **Random Run** — the F9 picker starts a full run: 10 random non-boss stages,
  then a random undefeated boss. There are 13 boss battles in the story data:
  worlds 1–5 stage 9 and 10, world 6 stages 1 and 2, and world 7 stage 10.
  World 7's final boss is always last. After each boss the next 10-stage block
  begins. `StoryUtility.initToNext` is overridden only while the active scene is
  `StoryBattle`, so StageSetup/WorldMap transitions still behave normally.
- **Session scoping** — randomizer effects (random characters, random
  enemies, random pickups, blank icons, pickup toasts, character-select skip,
  gem-continue skip) only run while a **Random Run** is active. Practice uses
  its own stats/loadout/repeat settings and ends its session when the stage
  resolves or the player returns to the world map/menu.
- **Random pickups** — `RandomPickups` moves map-fixed powerups (except keys and
  crystals) into random breakable blocks and sets `RandomPickupCount` random
  items per stage. `RandomPickupPool` controls the weighted pool:
  `Vanilla`, `Expanded` (all positive items), `Chaos` (fixed rare negatives),
  or `Difficulty` (default). `Difficulty` maps the run difficulty to negative
  item odds: Beginner 0%, Veteran 25%, Expert 50%, Master 50%. On Master,
  `[Gameplay] MasterStatReset` also resets starting stats. `HidePickupIcons`
  replaces the world model with a small neutral blank pickup until it is
  collected, and `PickupToast` shows the collected item's localized name above
  P1. All of this runs through the real `ItemController`/`Player.addItem` path,
  so achievements and counters still come from actual pickups.
- **Stats per stage** — `ResetStatsPerStage` re-applies the selected stats preset
  at the start of every stage. `Random` re-rolls, while Save keeps the current
  stats. `MasterStatReset` makes Master re-apply the selected preset every
  stage; with Save selected it falls back to Default. Default adds no items,
  so the character's base stats apply (most characters start at Fire 2 /
  Bomb 1). `CharacterData.items` is a list of `ItemTypedef.TYPE` ids, not a
  count table; the plugin writes UP/DOWN item ids to reach the requested final
  Fire/Bombs/Speed levels.
- **Practice tab** — the F9 menu starts on Beginner and has Practice,
  Randomizer, Multiplayer, Settings, and Help tabs. The selected row is highlighted in gold and the active tab is filled gold; each tab shows a
  Selected line with the current difficulty, stats preset and stage. Practice
  and Random Run keep separate Stats selections, so changing one does not
  change the other. A **Starting items** block edits the Custom preset
  directly: Fire/Bombs/Speed are bonus levels on top of the character's base,
  and the ability rows grant Punch, Power Glove, Kick, Remote, Penetration or
  Elasticity. Editing any entry switches Stats to Custom and persists through
  the `[Practice] Stats*` keys. Practice selects world and stage directly,
  re-applies its selected preset on every start and repeat, keeps the normal
  character-select screen (it never randomizes characters), and can **Repeat
  stage when cleared**. `[Practice] RepeatStage` stores the toggle.
- **F9 toggles** — the Randomizer tab toggles random pickups, blank icons,
  pickup-name popups, difficulty-based odds, random characters, random enemies,
  character-select skip, per-stage stat reset, Master stat reset, and gem
  continue skip. The Multiplayer tab toggles Smarter Bots, Random CPU
  Opponent, and CPU A-button specials. Toggles write the matching BepInEx
  config values. Character randomization is opt-in through the **Random
  characters** toggle; Practice never uses it.
- **Random CPU diagnostics** — when enabled, the log reports `Multiplayer:
  random CPU params=… com=… npcType=… aiType=…`, the candidate pool size, and
  how many CPU slots were randomized. This makes it clear whether the battle
  scene exposed COM slots and whether the AI-data filter found candidates.
- **In-game QoL menu** — the main menu **Other** entry is replaced with
  **Bomber QoL** and opens the same overlay as F9. `[UI] MainMenuQoL = false`
  restores the original Other entry. The Settings tab contains the runtime
  launcher toggles: motion blur, depth of field, bloom, vignette, grain, fast
  results, Master difficulty, movie skipping, auto-skip, soft reset, and BGM/SFX
  shuffle. The original game options remain available from the Settings tab.
  Folder paths, launch-time options, voices, BepInEx install, and save backups
  stay in the launcher. The launcher's **Game Folder** row lets you choose a
  non-Steam install; **Auto** restores library detection. Artwork extraction
  uses that same folder and prints its output path. Controller navigation uses D-pad/left stick, A/B, and
  L/R for tabs; raw joystick buttons are used as a fallback. Tabs have a scroll
  view that follows the selected row, and **X/Y + Up/Down** (or Shift+Up/Down,
  PageUp/PageDown) scrolls the current tab manually.

Both are exposed as launcher toggles. Practice saves remain suppressed, so this
is a safe base for a roguelike run: random characters, random enemies,
practice stage jumps and no real-save writes.

## Low-end PC mode

For weak GPUs, integrated graphics, Steam Deck, Proton/Wine, Winlator and other
PC-port layers, enable **Low-End Mode** in the launcher's Environment section
or the F9 Settings tab:

```ini
[Performance]
LowEnd = true
TargetFps = 60
HalfTextures = true
ShadowsOff = true
UltraLow = false
UltraWidth = 960
UltraHeight = 540
HighEnd = false
HighEndFps = 0
HighEndVSync = true
```

It applies at startup and on scene loads:

- 60 FPS target by default; optional 30 FPS cap (`TargetFps = 30`, `0` uncaps)
- VSync off
- anti-aliasing off
- shadows off and shadow distance 0
- half-resolution textures
- 1 pixel light
- lower LOD bias and maximum LOD level
- reduced particle raycast budget
- no soft particles or realtime reflection probes
- all post-processing effects forced off
- the in-game Options screen is updated to match (texture, AA, shadows,
  lights), so the saved game settings show the same values

**Ultra-Low Mode** goes further:

- sets the window to **960x540** (configurable)
- quarter-resolution textures (`masterTextureLimit = 2`)
- zero pixel lights
- LOD bias 0.4 and maximum LOD level 2
- particle raycast budget 0
- HDR/MSAA off on all cameras
- 60 FPS by default (optional 30 cap), no shadows, no post-FX

Ultra-Low is the **floor**: the window is clamped to at least 960×540.

**High-End Mode** is the **ceiling**:

- native resolution (restores the pre-ultra window)
- full-resolution textures (`masterTextureLimit = 0`)
- 8× anti-aliasing
- 8 pixel lights
- shadows on, shadow distance at least 150
- LOD bias 2.0 and maximum LOD level 0
- particle raycast budget 2000
- forced anisotropic filtering, soft particles and realtime reflections
- HDR/MSAA on for all cameras
- all post-processing effects on
- uncapped by default (`HighEndFps = 0`) with VSync enabled

High-End overrides Low/Ultra-Low if conflicting config values are present. The
launcher exposes all three switches and headless presets:

```bash
python3 bomber_launcher.py --low-end
python3 bomber_launcher.py --ultra-low
python3 bomber_launcher.py --high-end
./BomberLauncher.exe --low-end
./BomberLauncher.exe --ultra-low
./BomberLauncher.exe --high-end
```

Ultra-low changes the window resolution once at startup. The UI/text will be
blurry and the resolution persists until the game/desktop is restarted or the
in-game options change it. This is a performance profile for the existing PC
build, not a separate game build.

## Story game over

```ini
[Gameplay]
SkipGemContinue = true
MasterStatReset = true
```

When all player lives are gone, the story battle normally opens the gem
continue/retry dialog. With `SkipGemContinue`, the dialog is skipped and the
scene interrupts to `MainMenu` instead. The death text and jingle still play.
Set it to `false` to keep the normal gem retry flow.

## Launcher audio folders

The **Custom BGM Folder** and **Custom SFX Folder** rows show the selected
folder name, keep the full path in the tooltip, and include a **Clear** button.
The folder chooser opens at the current folder. Linux/GTK keeps a strong
reference to the native chooser so it cannot be garbage-collected before the
response arrives. The plugin retries audio decoding without a forced stream
type, which helps with some MP3/VBR files; `.ogg` and `.wav` remain the most
reliable formats.

## Multiplayer / local battle

```ini
[Bots]
SmarterBots = false

[Multiplayer]
RandomCpuOpponent = true
CpuSpecialAbilities = true
PlatformExclusives = false
```

- **SmarterBots** — postfixes `NPCAIData.getParam` for local-battle NPCs:
  `searchRange` is raised to at least 10 tiles, and
  `tacticalTargetParam.rethinkInterval` is capped at 0.25 s. This uses the
  game’s own utility-AI parameters; it does not alter story enemy AI,
  achievement counters, or the Weak/Normal/Strong NPC selection.
- **RandomCpuOpponent** — before a local battle starts, every `COM` slot gets a
  random usable character that has NPC AI data for the selected NPC difficulty.
  Human slots are untouched, duplicate picks are avoided, and the CPU status is
  set to that character’s normal status. This runs in
  `LocalBattleScene.mainStart`, before character packs are loaded.
- **CpuSpecialAbilities** — local-battle CPUs boost the `useSkill` desire and
  replace the special-skill process condition with an always-true condition.
  `NPCProcessSpecialSkill` still checks `Player/IStatus.haveSpecialSkill`, so
  only characters that actually have an A-button special ability will use one.
- **PlatformExclusives** — makes the built-in platform-gated IDs usable:
  `M001` Master Chief, `M002` Arbiter, `M003` Grunt, `S001` Ratchet & Clank,
  `S002` Toro, `S003` Piposaru, `V001` P-body, `V002` ATLAS, `N001` Max,
  `N002` Caesar. The Steam `chara_pack_03`/`04` assets contain their models,
  textures, and icons, and `CharacterVoiceDataTable` contains their voice keys.
  The plugin also injects display names for the M/S/N variants. They are
  player-selectable, but the `CharacterNpcAiInfoDataTable` only has 18 base
  characters, so platform exclusives are not selected as CPU opponents.
  Nintendo-specific models (Mario, Luigi, etc.) are not present in the Steam
  packs.

SmarterBots, RandomCpuOpponent, and CpuSpecialAbilities are local-battle only.
PlatformExclusives also affects the character-select and random-character
pools. Story enemies, online play, and achievement counters are not changed.

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
