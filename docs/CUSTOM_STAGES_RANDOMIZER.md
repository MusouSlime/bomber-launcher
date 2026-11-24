# Custom stages, story content and the randomizer

Scope for adding **new story/multiplayer content** and feeding it to the
randomizer. Based on the PC 2.2 data packs.

Short answer:

- **Remixing existing story maps is easy** and the randomizer picks the changes
  up automatically.
- **Adding new story stages is possible**, but needs new table rows, new flow
  entries and a small randomizer change.
- **Adding local multiplayer maps is possible** through `MultiMapInfoDataTable`.
- **New multiplayer modes/rules are not data-only**; they need code and UI.
- **Fully procedural maps are possible**, but that is a map-authoring project,
  not a table edit.

## What already exists

### Story

- 7 worlds.
- 62 `StoryMapInfoData` rows:
  - 49 normal stages,
  - 13 boss stages.
- Each stage points at a map data pack and asset:

```text
_mapPackName = map/data/map_data_pack_world03_02
_mapFileName = MapData_World03_02
```

- `StoryFlowDataTable_worldN` controls the order. Battle events are
  `eventType = 2`; each row has `storyFlowId`, `worldId`, `stageId`.
- `RandomRun.NormalCandidates()` currently hardcodes worlds 1–7 and stage 1–10
  (world 6 is capped at 2), then filters stages with `appearBossType != 0`.
  The pool is therefore the 49 normal stages.

### Map data

A `MapData` row is editable data, not a baked scene. Important fields:

```text
_mapSize                     (x, y, z)
_blocks[]                    BlockData: index, children, itemType,
                             direction, resourceTypeHandle.index,
                             enemyGateSpawnInterval, ...
_playerStartPointDatas[]     position + direction
_enemyStartPointDatas[]      enemy name + position + direction
_useEnemyList[]              enemy palette for this map
_bossStartPoint              boss position
_goalPoint                   goal position
_escortTargetStartPointDatas[]
_absItemCounts, _itemMask, _randomItemMask, _randomItemMin/Max
_checkPoints[], _routeSearchAreas[], _camTargetAreas[]
```

`resourceTypeHandle.index` indexes `MapResourceData._mapResources`, where each
entry has a name such as `HARD_A_1`, `END_1`, `SOFT_BLOCK_1`, `PILLAR_A_1`,
`ENEMY_GATE_A_1`, `OPEN_GOAL_SWITCH_1`, etc. A new map can reuse an existing
world's block pack and resource table.

The map data pack is an AES UnityFS bundle like every other PC pack; it can be
decrypted, edited and re-encrypted with `tools/sbr_pack.py`.

### Multiplayer

`MultiMapInfoDataTable_builtin` has 60 local/online map rows. Each row has:

```text
_mapId, _contentType, _mapName
_mapPack, _mapPath
_envPack, _envPath
_thumbnailPack, _thumbnailPath, _thumbnailMiniPath
_normalBgmPackName/_normalBgmFileName
_pinchBgmPackName/_pinchBgmFileName
_normalCameraParamName/_resultCameraParamName
_releaseId, _maxPlayer, _displayType, _randomSelectWeight
_equipmentItem, _gemsAcquisitionConditionKeys
```

A new map row can reuse an existing env, thumbnail and BGM, or add its own.
Local random map selection already honours `_randomSelectWeight`.

## Feasibility matrix

| Goal | Route | Effort | Notes |
|---|---|---|---|
| Remix an existing story map | Edit `MapData_*` in a map pack | 1–3 days | Randomizer and story use it automatically |
| Change enemies/items on a story map | Edit `_useEnemyList`, item masks/counts | Hours | No new IDs or tables |
| Add a new normal story stage | New map pack + `StoryMapInfoData` + `StoryFlowData` row + randomizer pool entry | 1–2 weeks | Best in free IDs, e.g. world 6 stages 3–10 |
| Add a new boss stage | Above + `appearBossType`, boss pack/AI/voice | 2–4 weeks | Boss flow and final-boss ordering need care |
| Add a local multiplayer map | New map pack + `MultiMapInfoDataTable` row + thumbnail | 1–2 weeks | Thumbnail can be reused initially |
| Add a new multiplayer rule/mode | `BattleDef`/`GPBattleRule*` + UI + code | Weeks–months | Not data-only |
| Procedural/custom geometry | Map generator that writes `MapData` | 2–4 weeks for a tool, then per-map | Highest payoff, highest risk |
| Custom story dialogue/chapters | Story flow, interlude/movie and locale data | High | Out of scope for the randomizer |

## Recommended technical routes

### Route A — remix existing map packs

This is the lowest-risk proof and gives the randomizer new content immediately:

1. Decrypt a map data pack, e.g. `map_data_pack_world03_02`.
2. Clone `MapData_World03_02` and edit:
   - `_blocks` layout,
   - player/enemy start points,
   - `_useEnemyList`,
   - item counts/masks,
   - goal/escort points.
3. Re-encrypt the pack with the original file name.
4. Install it as a patch pack.

No table edits and no plugin changes are needed. The randomizer already picks
world 3 stage 2 from its fixed pool.

This route is also the best way to add **cross-world enemy palettes**: put
enemies from other worlds in `_useEnemyList`, and the existing random-enemy
feature can shuffle them.

### Route B — add new stage slots

Use free stage IDs so the normal 1–10 flow is not disturbed. World 6 is the
cleanest candidate:

- World 6 only has stages 1–2, both bosses.
- Stages 3–10 are unused.
- A new normal stage at 6/3 can be added without colliding with story
  progression.

Work:

1. Create a map data pack for each custom stage.
2. Add `StoryMapInfoData` rows for world 6 stages 3–10, with
   `appearBossType = 0`.
3. Add `StoryFlowData` battle rows (`eventType = 2`) for those stages.
4. Ensure each custom battle has a valid preceding flow row; the randomizer
   calls `story.setWorldId(world, flowId - 1)`, so the flow index semantics
   must be tested rather than assumed.
5. Extend `RandomRun.NormalCandidates()` to include the custom slots, or read a
   configurable `ExtraStages` list. A simple first version can change the
   world-6 cap from 2 to 10.
6. Add an F9/launcher option: **Include custom stages**.

The custom stages can be randomizer-only. The normal world map and story
progression do not need to show them.

### Route C — map generator

A Python tool can generate `MapData` by cloning a template and replacing
`_blocks`:

1. Load a template map and its `MapResourceData`.
2. Build a grid:
   - floor blocks,
   - perimeter walls,
   - soft blocks,
   - pillars/gimmicks,
   - player spawns,
   - enemy palette/spawns,
   - goal and items.
3. Serialize a new `MapData` MonoBehaviour into a new pack.
4. Re-encrypt.

The hard part is not the grid; it is validating block `children`, resource
indices, map bounds and spawn orientation in-game. Start with a flat arena
using one world's existing block set, then add themes and gimmicks.

`MapData` is not a scene, so a generator does not need the Unity editor. It
does need a reliable UnityPy write/save path for the custom struct arrays.

### Route D — multiplayer maps

1. Create a map data pack (Route A or C).
2. Add a `MultiMapInfoData` row:
   - unique `_mapId`,
   - `_mapPack`/`_mapPath`,
   - reuse an existing env pack and thumbnail initially,
   - `_maxPlayer = 4`,
   - `_randomSelectWeight`,
   - camera/BGM/equipment keys from a similar map.
3. Inject the row at runtime or replace `MultiMapInfoDataTable_builtin` in a
   patch pack.
4. If the map select ignores appended rows, patch its map count/list methods.

Keep custom multiplayer maps **local-only**. Other players in an online or
Grand Prix room do not have the map, so the match will desync or fail to load.
A custom mode needs `BattleDef` data, rule UI and code; do not scope it as a
map pack.

## Randomizer integration

The randomizer currently works from stage coordinates, not from arbitrary map
rows. Three levels of integration:

1. **Replace existing maps** — no changes; the pool is unchanged but the
   content is new.
2. **Add custom slots inside the existing coordinate range** — e.g. world 6
   stages 3–10; change the candidate range or add an explicit extra list.
3. **Read `StoryMapInfoDataTable` dynamically** — more flexible, but requires
   filtering custom/boss/test rows and handling UI stage IDs that are outside
   the normal 1–10 range.

Recommended first version: custom slots in world 6 with an explicit
`ExtraStages` config list. That keeps the randomizer deterministic and easy to
debug.

Additional randomizer content that is cheaper than new geometry:

- **Enemy palette mixing** — put any enemy names in `_useEnemyList`.
- **Item chaos** — edit `_absItemCounts`, `_itemMask`, `_randomItemMask`
  and min/max.
- **Gimmick remixes** — reuse existing block resources (conveyors, switches,
  shafts, slippery floors, enemy gates).
- **Boss rush** — reuse existing boss stages in a new run order; already close
  to what Random Run does.
- **Custom run rules** — plugin-side options (all enemies, no items, fixed
  character, etc.); code only, no map authoring.

## Risks and constraints

- **Map data serialization** is the main unknown. UnityPy can read the fields;
  writing modified struct arrays and re-encrypting must be tested before
  promising a generator.
- **Flow IDs** are not arbitrary. `setWorldId(world, flowId - 1)` assumes a
  specific preceding event; custom flow rows need to follow the existing
  pattern.
- **Stage IDs**: the normal world map/UI assumes stages 1–10. Custom stages
  should be randomizer-only or use free slots such as world 6 stages 3–10.
- **Boss detection** uses `appearBossType`; keep it 0 for normal custom stages.
- **Progression and achievements**: Random Run already suppresses story saves
  (per the existing behavior), so custom random stages should not corrupt story
  progress. Normal story access would need explicit progression handling.
- **Multiplayer**: custom maps/modes must be local-only unless every client has
  the same patch.
- **Asset/legal**: distribute patch map packs, not full game dumps.
- **Performance**: large custom maps and extra enemy types cost memory; test
  with low-end/ultra-low modes.

## Effort summary

| Scope | Effort |
|---|---|
| Remix one existing story map + enemy/item changes | 1–3 days |
| Add 1–3 custom normal stages in free slots + randomizer pool | 1–2 weeks |
| Map generator for flat arenas using existing block sets | 2–3 weeks |
| Themed generator with gimmicks and validation | 4–6 weeks |
| Local multiplayer map row + pack | 1–2 weeks |
| New multiplayer mode/rule | Not scoped; code/UI project |

## Recommended order

1. **Remix one story map** and verify the randomizer picks it up. This proves
   map pack editing and gives immediate content.
2. Add **custom enemy palettes and item settings** to existing maps. This is
   high variety for very little work.
3. Add **one custom normal stage in a free world-6 slot**, with an `ExtraStages`
   entry in the randomizer.
4. Expand to a small custom-stage set.
5. Add **one local multiplayer map** using an existing env/thumbnail.
6. Only then decide whether a full map generator and new modes are worth it.

## Open questions

1. Can modified `MapData` struct arrays be saved and re-encrypted reliably with
   UnityPy?
2. What exactly does `BlockData.children` mean for multi-cell blocks, and which
   block resources are safe for a flat arena?
3. Does `setWorldId(world, flowId - 1)` behave correctly for appended flow
   rows, or do custom rows need a preceding event?
4. Are world-6 stages 3–10 truly unused by story flow and UI?
5. Should custom stages be randomizer-only, or also accessible from Practice?
6. Do we want custom maps in local multiplayer only, or also in GP offline?
