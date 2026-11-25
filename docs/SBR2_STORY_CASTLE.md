# SBR 2 story: removing Castle sections and replacing them

Short answer: **removing them is feasible** (data + clear flags), and
**replacing them with boss/enemy content is feasible with a code hook**.
Replacing them with true classic Battle Mode is a bigger project because the
story flow has no "standard battle" node.

This is based on `defines/common` and the IL2CPP dump of `StoryManager`,
`StoryExploreLogic`, and the story data tables.

---

## 1. What the “Castle sections” actually are

The story has **two** Castle layers, both side events layered on top of the
normal explore/camp/boss flow:

### Castle attacks (raid the enemy castle)

- **4 per world**.
- `StoryEventDataTable_World1/2/3`:
  - World 1: event IDs 1–4, `_eventType = 1` (`StoryDef.EVENT_TYPE.CASTLE_ATTACK`), `_typeId` 1–4
  - World 2: event IDs 0–3, same pattern
  - World 3: event IDs 1–4, same pattern
- `StoryCastleAttackOccurrenceConditionsTable_World1/2/3` maps each attack to:
  - `_castleAttackId` 1–4
  - `_mapId` 1–4
  - `_shutterId` (map shutter that triggers it)
  - `_npcIds` (Castle NPCs)
- `StoryExploreLogic.AbstractBattleState.processStoredEvent()` checks
  `eventType == 1` and calls `StoryManager.getCastleAttackParam(...)`, which
  builds the Castle attack scene param.

### Base defence (defend your own base)

- **4 per world**.
- `StoryBaseDefenceOccurrenceConditionsTable_World1/2/3`:
  - `_needMinionCount` thresholds, e.g. World 1: **1 / 10 / 30 / 50**
  - `_baseDefenceId` 1–4
  - `_npcIds` and `_ruleSet`
- `StoryBaseDefenceOccurrenceAreaGateDataTable_World1/2/3` maps area
  transitions where a base defence can trigger.
- `StoryExploreLogic.ExploreBattleState.execEndAreaChangeWait()` and
  `StoryManager.createCampParam()` call `checkBaseDefenceEvent(...)`.

### Main story flow does *not* contain Castle nodes

`StoryFlowDataTable_World1/2/3` is only:

- world map, movies, camp, explore, tutorials
- `PREV_BOSS` (`6`), `BOSS` (`7`), `WORLD_CLEAR` (`8`)
- `LAST_BATTLE` (`13`) in World 3

`StoryManager.EVENT_TYPE` does define `CASTLE_DEFENCE = 4` and
`CASTLE_OFFENCE = 5`, but they are **not present** in the shipped flow tables.
Castle is an interrupt/side activity, which is why removing it is plausible.

---

## 2. What breaks if you just delete the events

`StoryManager` tracks the Castle state outside the flow:

- `WorldStrategyStatus.attack[]` — castle attack states
- `WorldStrategyStatus.defence[]` — base defence states
- `isClearedAllAttackAndDefence(worldId)` — called from
  `StoryExploreLogic.CastleBattleState.execEnd()` and
  `StoryManager.createCampParam()`
- `checkBaseDefenceEvent(worldId, ...)` — called from
  `StoryExploreLogic.ExploreBattleState.execEndAreaChangeWait()` and camp
- `SaveStoryData.DataVersion000.WorldData` stores:
  - `clearBaseDefenceCount` / `clearBaseDefenceIds`
  - `openBaseDefenceIds` / `baseDefenceAreaGateBits`
  - `clearCastleAttackCount` / `clearCastleAttackIds`
  - `newClearCastleAttackCount` / `newClearCastleAttackIds`

So a data-only deletion can leave the camp/strategy state thinking Castle is
still pending, or leave base-defence area gates closed. The safe skip is:

1. suppress the triggers;
2. mark all 4 attacks and 4 defences as cleared;
3. mark the base-defence area gates used/opened;
4. save/reload and test the camp + next boss.

`StoryManager` exposes public methods for this:

- `setClearCastleAttack(worldId, castleAttackId)`
- `setClearBaseDefence(worldId, baseDefenceId)`
- `setOpenBaseDefence(worldId, baseDefenceId)`
- `setBaseDefenceAreaGateUsed(worldId, gateId)`

A BepInEx 6 plugin can call these on story load; that is more reliable than
editing the binary save.

---

## 3. What “remove” looks like in practice

### Data side

`patches/sbr2-no-castle.json` (experimental):

- Sets `StoryEventDataTable_World1/2/3` events 1–4 `_eventType` to `0`
  (`INVALID`) and `_typeId` to 0.
- Sets `StoryBaseDefenceOccurrenceConditionsTable_World*`
  `_needMinionCount` to `999999` so base defence does not trigger.
- Does **not** delete the occurrence tables, so IDs/rewards remain valid.

Apply to a copy first:

```sh
python3 tools/sbr2_data_patch.py \
  --patch patches/sbr2-no-castle.json --dry-run

python3 tools/sbr2_data_patch.py \
  --patch patches/sbr2-no-castle.json \
  --out /var/home/user/.cache/sbr2-no-castle
```

### Runtime side (required for a clean skip)

A small `StoryNoCastle` BepInEx 6 IL2CPP plugin:

```csharp
// pseudo-code: call after StoryManager has loaded a save / entered story
var story = StoryManager.instance;
for (int world = 0; world < 3; world++) {
    for (int id = 1; id <= 4; id++) {
        story.setClearCastleAttack(world, id);
        story.setClearBaseDefence(world, id);
    }
    // gateCount comes from StoryBaseDefenceOccurrenceAreaGateDataTable_World*
    // (World 1: 5, World 2: 6, World 3: 5)
    for (int gate = 0; gate < gateCount; gate++)
        story.setBaseDefenceAreaGateUsed(world, gate);
}
```

Also hook `StoryExploreLogic.AbstractBattleState.processStoredEvent` or
`StoryManager.getCastleAttackParam` as a safety net: if a Castle event still
slips through, skip it instead of loading the Castle scene.

### Test checklist

- World 1: first shutter where Castle attack #1 used to fire
- Camp after 1 / 10 / 30 / 50 minions (base defence thresholds)
- Every world transition and boss
- World clear and last battle
- Save/reload mid-world
- Rewards, Fusel notes, achievements tied to Castle/base defence
- If a base-defence area gate stays closed, mark that gate ID used and retest

---

## 4. Replacing Castle with more Bomberman gameplay

The story’s playable node types are explore maps, puzzles, boss fights, and
movie/camp nodes. There is no standard Battle Mode node, so “replace” splits
into four options.

### Option A — Replace each Castle with a boss fight (medium)

Reuse existing boss content. Two ways:

1. **Data experiment:** change the four `CASTLE_ATTACK` events to
   `BEGIN_BOSS` (`StoryDef.EVENT_TYPE.BEGIN_BOSS = 2`) with a valid `typeId`.
   `BEGIN_BOSS` events already exist in World 2/3, so the handler exists.
   Whether it starts a real boss fight or only a pre-boss cutscene must be
   tested per ID.
2. **Code hook:** when `processStoredEvent` sees a Castle event, call a custom
   method that builds a boss/`StoryExploreLogic.LogicParam` instead of
   `getCastleAttackParam(...)`. This is more reliable than guessing `typeId`
   semantics.

Effort: medium; needs boss/area ID mapping and result/progression testing.

### Option B — Replace each Castle with an enemy maze (medium-hard)

Redirect the Castle event to a normal story explore/battle map:

- Hook `StoryManager.getCastleAttackParam(...)`.
- Return a `StoryExploreLogic.LogicParam` for a `str` map/area with normal
  enemies/puzzles instead of a Castle attack param.
- Mark the Castle ID cleared when the battle ends so camp/progression advances.

This reuses existing `map/data/str/*` content. It is the closest to “more
Bomberman-like” without making new maps.

Effort: medium-hard; the param construction and result handoff need reverse
engineering.

### Option C — Replace each Castle with classic Battle Mode vs bots (hard)

Launch a standard battle scene (`RuleStd`/Battle 64-style) from the story
event, then return a win/lose result to `StoryManager` and mark the Castle ID
cleared.

Problems:

- story code expects explore/castle result data, not a standard battle result;
- no standard-battle story node exists;
- rewards/camp state must be updated manually.

Effort: hard; possible but a separate mini-project.

### Option D — New story maps/content (hard)

Build new `str` maps (or reuse the stage editor/map bundles), add them to
`World*MapInfoDataTable`, and insert flow/explore nodes. Highest effort;
requires map/area IDs, save flags, and puzzle/boss logic.

---

## 5. Recommended path

1. **Ship “skip Castle” first.**
   - `patches/sbr2-no-castle.json` + `StoryNoCastle` plugin clear flags/gates.
   - This makes the story explore → puzzle → boss, with no asymmetric Castle
     interruptions.
2. **Then replace with bosses** if “skip” feels too empty.
   - Test `BEGIN_BOSS` data changes one event at a time; fall back to a code
     hook if it only plays a cutscene.
3. **Then consider enemy-maze replacement** if you want more normal Bomberman
   gameplay between bosses.
4. **Leave classic Battle Mode integration for later**; it is a bigger
   architecture change than a data patch.

The important safety point: do not delete Castle data and assume the camp will
ignore it. Mark the attack/defence IDs cleared and the base-defence gates used,
or the story can soft-lock at a camp/area transition.
