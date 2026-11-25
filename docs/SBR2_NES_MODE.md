# Bomberman NES gameplay loop — research and OG Mode spec

Status: research + design. Layout generator started in
`tools/og_mode_gen.py`; no SBR2 MapData bundles or plugin code yet.

Working name: **OG Mode** — the 1985 NES loop as a 50-stage arcade mode.

This document answers two questions:

1. What is the actual gameplay loop of the 1985 NES/Famicom *Bomberman*?
2. What does a faithful OG Mode inside SBR2 need to be?

The short version: **the NES game is not 50 hand-authored stages. It is one
compact rule system, one long horizontal field, eight enemy behaviors, and a
stage generator.** That is why it ports well as a *mode* and scales to 50 stages
without 50 maps of content work.

---

## 1. What the NES game actually is

Sources disagree on details, but the core is consistent:

- 1985 Famicom / 1989 NES, single-player, maze-like action game.
- **50 levels**, each a long horizontal field that scrolls left-to-right.
- Goal: escape the underground facility and reach the surface.
- Each level: destroy soft blocks, kill every enemy, find the hidden exit,
  enter it.
- One power-up per level, hidden under a soft block.
- One secret bonus item per level, under special conditions.
- A countdown timer. When it expires, extra enemies spawn.
- Three lives, plus one extra life for every level cleared.
- Password system for continuing.
- Eight enemy types with distinct movement/chase behavior.
- Occasional bonus stages between levels.

The field is a **long landscape rectangle**: a **29 × 11 interior** inside a
one-tile hard border, i.e. **31 × 13 tiles total**. The VGMaps stage atlas
measures the stage 1 interior at 464 × 176 px at 16 px per tile = 29 × 11; the
hard border adds one tile on each side. The NES screen shows 16 tiles at a
time, so the stage scrolls horizontally — it is not a single static screen.

The hard skeleton is a **14 × 5 pillar lattice** at even coordinates. The
bottom-left start pocket removes one pillar, leaving 69 hard blocks, which is
exactly what the atlas shows. `tools/og_mode_gen.py` builds this base and then
randomizes the soft blocks and enemies.

SBR2's battle grid uses the same X/Z grid convention. A stock SBR2 versus map
(`map_data_vs001`) is 15 × 13 with a one-tile border and interior starts at
(1,1)/(13,11); another (`vs002`) is 19 × 13. SBR2 story maps go up to 88 × 68.
A **31 × 13** OG Mode field is therefore within `MapData`'s range, but the
versus camera/mode is built for smaller arenas; OG Mode needs a horizontally
scrolling battle camera.

---

## 2. The loop, step by step

```
      spawn in a safe pocket
                |
                v
   bomb soft blocks to open the map
        |                    |
        |                    +--> find the one power-up
        |                    |       (or destroy it -> guardian enemy)
        |
        +--> kill every enemy
                |
                v
        exit opens (it was hidden under a soft block)
                |
                +--> if a blast hits the exposed exit -> more enemies
                |
                v
        walk into the exit -> next stage (+1 life)
                |
                v
        death -> lose temporary power-ups, retry stage
                |
                v
        timer 0:00 -> Pontans pour out of the exit
```

The important detail is that the NES game rarely kills you with a surprise. It
**converts mistakes into more enemies**:

- Bomb the exit? Enemies spawn from it.
- Bomb the power-up? Its guardian enemy spawns.
- Run out of time? Pontans spawn.
- Die? You keep only the permanent upgrades and lose the temporary ones.

That conversion is the whole design. It makes the player's own tools create the
next problem, without needing new mechanics per stage.

---

## 3. Exact rules worth preserving

| Rule | NES behavior | Why it matters |
| --- | --- | --- |
| Field | 29 × 11 interior, 1-tile hard border, 31 × 13 total; horizontal scroll | Short runs with a readable wide field |
| Start | Bottom-left safe pocket | Consistent first decision |
| Hard blocks | Indestructible border and per-stage hard shapes | Permanent maze structure |
| Soft blocks | Destructible, hide the exit and the power-up | The map is a consumable resource |
| Enemies | Fixed roster per stage, all must die | Clear, unambiguous objective |
| Exit | Hidden under one soft block; opens when enemies = 0 | The last discovery is the goal |
| Exit bombing | A blast on the exposed exit spawns enemies | Punishes careless clearing |
| Power-up | Exactly one per stage, hidden under a soft block | Strong risk/reward |
| Power-up destruction | A blast on the power-up destroys it and spawns its guardian | The upgrade can become a threat |
| Permanent upgrades | Bomb Up and Fire Up survive death | Long-term progression |
| Temporary upgrades | Remote, Wall Pass, Bomb Pass, Flame Pass, Speed Up, Invincible are lost on death | Attrition pressure |
| Max stats | 10 bombs, 10 fire | Bounded power curve |
| Timer | Countdown; timeout spawns Pontans | Prevents endless turtling |
| Lives | Start at 3; +1 per stage cleared | Steady but forgiving |
| Password | Resume at a stage | 50-stage runs need a checkpoint |

The NES item list (from the game's own item table):

- **Bomb Up** — max bombs +1 (cap 10), permanent.
- **Fire Up** — blast radius +1 (cap 10), permanent.
- **Speed Up** — movement speed up. NES only places it on stage 4; lost on death.
- **Remote Control** — manual detonation with B.
- **Wall Pass** — walk through soft blocks.
- **Bomb Pass** — walk through bombs.
- **Flame Pass** — survive your own blasts.
- **Invincible / Vest** — 30 seconds of immunity.

Secret bonus items (B Panel, Goddess, Cola, Famicom, Programmer Nakamoto,
Dezeniman) are score Easter eggs with obscure conditions and huge point values.
They are optional for the port; they are not part of the core loop.

---

## 4. The eight enemies

This is the actual difficulty curve of the NES game. The stage number changes
which behaviors are in play, not the rules.

| Enemy | Debut | Speed | Movement / chase | Soft-block pass | Notes | Score | Guardian item |
| --- | --- | --- | --- | --- | --- | ---: | --- |
| **Ballom** | 1 | 0.5× player | Random wander; 20/256 chance to chase | No | The tutorial enemy; slow and unpredictable | 100 | Bomb Up |
| **Onil** | 2 | 1.0× player | Random; 150/256 chance to chase, **columns only** | No | Fast and awkward; its axis quirk is real | 200 | Fire Up |
| **Dahl** | 3 | 1.0× player | Row-biased; 150/256 chance to chase, **rows only** | No | Dumb, gets stuck; horizontal pressure | 400 | Speed Up |
| **Minvo** | 4 | ~1.2× player | 200/256 chance to chase on both axes | No | First genuinely aggressive hunter | 800 | Wall Pass |
| **Doria** | 7 | 0.25× player | 200/256 chance to chase; evades bombs | **Yes** | Slow ghost; ignores your maze | 1,000 | Remote Control |
| **Ovape** | 10 | 0.5× player | Ballom AI + soft-block pass | **Yes** | Slow but cannot be walled out | 2,000 | Bomb Pass |
| **Pass** | late | ~1.2× player | Always chases; bomb-smart | No | Relentless; the "smart" enemy | 4,000 | Vest / Flame Pass |
| **Pontan** | 49–50 + timeout / exit spawn | ~1.2× player | Always chases | **Yes** | The punishment enemy; fastest and unstoppable | 8,000 | none |

Notes:

- The scores form a clean danger ladder: 100 → 8,000.
- Doria and Ovape are the "ghost" enemies: they ignore soft blocks, so clearing
  the map does not protect you.
- Onil's column-only chase and Dahl's row-only chase are documented quirks in
  the NES code, not just flavor. They are cheap to implement and make the
  enemies feel distinct.
- The exact debut stage of Pass is not consistently documented; treat it as a
  late-game enemy.
- The guardian item column is the NES association used when the exit or item is
  bombed. The Pass/Pontan item naming is slightly ambiguous in the sources
  (Vest / Flame Pass / Invincible overlap); the port should define one
  unambiguous table.

---

## 5. Why this loop still works

### 5.1 Compression

One long horizontal field, a handful of enemies, one pickup, one exit. A stage
takes one to three minutes. The player always knows the objective, and the field
is simple enough to read while it scrolls. That is ideal for a pick-up-and-play
mode.

### 5.2 The soft blocks are the real level design

Soft blocks do three jobs at once:

1. they are the maze,
2. they hide the exit,
3. they hide the only power-up.

Every bomb is therefore progress and risk. Clearing space exposes you; bombing
blindly can destroy the exit's cover or the item you wanted.

### 5.3 Mistakes become enemies

This is the elegant part. The NES game does not use fail-states for mistakes; it
uses **enemy conversion**:

- exit + blast → more enemies,
- item + blast → guardian enemy,
- timer + 0 → Pontans.

The player is never simply told "you failed". The board gets harder. That is
much better than a game-over screen and it is easy to implement.

### 5.4 Attrition, not difficulty spikes

You start with three lives and gain one per stage. Temporary power-ups vanish
on death. Permanent Bomb Up / Fire Up accumulate. The run gets easier in raw
power but the enemy mix gets harder. That is a smooth 50-stage curve without
bosses.

### 5.5 Randomization with a fixed rule set

Soft-block placement is randomized; the enemy roster and objective are fixed.
The player learns the rules, not the map. That is why 50 stages is cheap
content: the generator provides replayability and the enemy table provides
progression.

### 5.6 No bosses, no story, no new mechanics

The NES game has no boss fights and no story scenes during stages. Difficulty
comes from density, enemy combinations, and map pressure. For a port, this is a
feature: there is no second game to build.

---

## 6. What the SBR2 version should be

**A separate arcade mode, not a story chapter and not a museum stage pack.**

Working name: **OG Mode**.

It is:

- one long rectangular stage at a time,
- kill all enemies,
- one hidden pickup,
- hidden exit,
- timer/lives/score,
- 50 generated stages,
- local single-player or 2-player co-op.

It is not:

- open-world exploration,
- camp/Ellon progression,
- story cutscenes,
- bosses,
- SBR2's modern item/skill system.

The museum plan can still host it as a "classic game" entry, but the mode
itself is self-contained.

---

## 7. Mapping the NES loop onto SBR2

SBR2 already has most of the low-level pieces. The gaps are the **PvE rule**
and the **enemy behavior layer**.

| NES element | SBR2 system | Fit | Gap |
| --- | --- | --- | --- |
| 31 × 13 grid | `MapData` with X/Z grid, Y layers | Good | None for the data; the mode needs a horizontal scroll camera |
| Hard border / pillars | `MapData.BlockDeployParam`, block stats (`END_10_A`, `HARD_05_A`, `PILLAR_05_A`) | Good | None |
| Soft blocks | `SOFT_10_A` block deploy params | Good | None |
| Player / bombs / blast | Existing battle player and bomb controllers | Good | None |
| One pickup | `BlockDeployParam.itemParamWhenDeploy` (`itemAppearRate`, `itemType`) | Good | Custom destruction/guardian rule |
| Enemy prefabs | `EnemyInfoDataTable` (13 archetypes: WANDERING1–3, RUSH1–3, SHOOTING1–3, JUMP1–2, BOMB_EAT1–2) | Partial | No ghost/soft-block-pass, no axis-locked chase |
| Enemy spawning | `MapData.EnemyStartPointParam` | Partial | Standard vs maps have no enemies; story modes spawn through story logic |
| Kill-all objective | None in standard battle; story uses explore/puzzle goals | **Gap** | Needs a custom rule or overlay mode |
| Exit / door | Story uses checkpoints/goal shutters; vs has no door | **Gap** | Needs a custom goal object |
| Timer / lives / score | Battle timer exists; lives/score are mode-specific | Partial | Custom HUD/state |
| Stage select / save | SBR2 save system is story/battle-specific | **Gap** | Custom sidecar save or stage manifest |
| Per-stage BGM | `MapInfoDataTable.bgmId` / custom BGM plugin | Good | None |
| 50 stages | No existing NES content | Content gap | Solved by generator, not 50 hand maps |

Concrete anchor from the installed game:

- `map_data_vs001`: `mapSize = (15, 40, 13)`, 195 ground blocks at `y=0`,
  167 blocks at `y=1`, player starts at `(1,1)`, `(13,1)`, `(1,11)`, `(13,11)`.
- Block stats: `HARD_05_A` (ground), `END_10_A` (border), `HARD_05_C`,
  `SOFT_10_A` (destructible), `PILLAR_05_A`.
- `EnemyInfoDataTable` has 13 entries with `_id`, `_type`, `_life`,
  `_moveSpeed`, `_movePattern`, `_moveMethod`, `_playerSearchDistance`,
  `_actionToPlayer`, and a prefab name.

That means a NES map is a normal SBR2 map with a different block layout. The
hard part is not the map; it is the mode logic.

---

## 8. Two architectures

### Option A — native rule integration

Reuse SBR2's `EnemyController`, `RuleStd*` / `RuleStr*`, and battle lifecycle.
Write a custom rule that:

- spawns enemies from map data,
- counts enemy deaths,
- opens a goal when the count reaches zero,
- handles the exit blast and timer spawns.

**Pros:** reuses enemy AI, animations, collision, sound, network sync.
**Cons:** SBR2's rule framework is built around battle/story modes; a custom
PvE rule may fight the lifecycle. Enemy spawning outside story maps is not
proven. Network-synced rule masters are a lot of surface area.

### Option B — overlay arcade mode

Start an offline battle map, keep the normal player/bombs/camera, and run a
custom OG Mode controller on top:

- spawn enemies as prefabs with our own grid AI,
- detect bomb blasts and kill enemies in blast cells,
- spawn the exit door and the pickup,
- track kills, lives, timer, score,
- load the next stage from the manifest.

**Pros:** exact NES behaviors (ghost, axis-lock, bomb-smart), no dependence on
SBR2's story/rule internals, easy to iterate, works offline.
**Cons:** we own enemy movement/collision/animation; more code than reusing
`EnemyController`; must hook bomb explosions cleanly.

### Recommendation

**Option B for v1.** The NES enemies are simple grid agents; writing eight
behaviors is smaller and more predictable than bending SBR2's rule system. It
also gives exact fidelity for Doria/Ovape/Pontan (soft-block pass) and
Onil/Dahl (axis-locked chase), which the native enemy table does not expose.

If SBR2's enemy prefabs cannot be spawned standalone, use SBR2 minion/enemy
models as visual shells and drive them with our own grid movement. The mode
does not need SBR2's enemy AI.

### The one spike that decides everything

> Can we start an offline SBR2 battle map, keep the normal player and bombs,
> spawn six custom grid enemies, detect a bomb blast on them, and open a door
> when the count reaches zero?

If yes, the rest is content and polish. If no, fall back to Option A.

---

## 9. Stage generation spec

The 50 stages are generated, not authored. One ruleset, one generator, one
manifest.

### 9.1 Grid

- Total: **31 × 13**.
- Border: 1 tile of hard blocks.
- Interior: **29 × 11**.
- Ground layer: all cells filled at `y=0` (SBR2 convention).
- Play layer: border, hard pillars, soft blocks at `y=1`.
- Player start: `(1, 10)`, bottom-left, facing into the field.
- Safe pocket: clear a 3 × 3 area around the start.
- Camera: horizontal scroll. The field is roughly two NES screens wide.

### 9.2 Hard blocks — the one repeated map

OG Mode uses **one base map** for all 50 stages:

- Hard border around the 29 × 11 interior.
- **14 × 5 pillar lattice** at even X / even Z (70 pillars).
- The 3 × 3 bottom-left start pocket removes one pillar, leaving **69 hard
  blocks**.

This is the NES stage-1 skeleton measured from the atlas, and it is the "same
map" that repeats every stage. Only the soft blocks, enemies, exit and pickup
change.

### 9.3 Soft blocks

- Stage 1: **55 soft blocks** (the NES stage-1 count).
- Rising to **~128** by stage 50, out of ~250 free cells.
- Never fill the 3 × 3 spawn pocket.
- Keep a few open lanes so enemies can move before the player clears the map.
- The exit and the pickup are each placed under a random soft block. The exit
  is at least 9 tiles from the spawn; the pickup is at least 6 tiles from the
  exit.

### 9.4 Exit and pickup

- **Exit:** one soft block is flagged as the exit. When that block is destroyed,
  a door object is revealed. The door is locked until `enemiesRemaining == 0`.
- **Exit blast:** any explosion that overlaps the revealed exit spawns one
  Pontan (or a stage-scaled number) and increments the enemy count.
- **Pickup:** one other soft block contains the stage's power-up. When the block
  is destroyed, the item appears.
- **Item blast:** an explosion that overlaps the revealed item destroys it and
  spawns its guardian enemy. This is the NES rule. Make it a config toggle
  ("destructible items") so casual players can turn it off.
- **Pickup effect:** apply the item to the player. Permanent items persist
  across stages; temporary items are cleared on death.
- **Power-up table:** the 50-stage NES table is extracted from the ROM at PRG
  offset `0xC04` (CPU `$CC03,X`, 1-indexed by stage). It is used verbatim by
  `tools/og_mode_gen.py`.

  ```
  2,1,5,3,1,1,2,5,6,4, 1,1,5,6,2,4,1,6,1,5,
  6,5,1,5,6,8,2,1,5,7, 4,1,5,8,6,7,5,2,4,8,
  5,4,6,5,8,4,6,5,7,8
  ```

  With the NES item order (`1=Bomb Up, 2=Fire Up, 3=Speed Up, 4=Remote,
  5=Wall Pass, 6=Bomb Pass, 7=Flame Pass, 8=Invincible`), stage 4 = 3 =
  Speed Up, which matches the documented NES stage-4-only Speed Up. That is the
  independent check that the table is the per-stage power-up table.

### 9.5 Enemies

- Stage 1: **4 enemies**; rising to **10** by stage 49–50.
- Place on floor cells at least 7 tiles from the spawn.
- One main type per stage, with a 25 % chance of a second unlocked type from
  stage 6 onward.
- Stage 49 adds one Pontan; stage 50 adds two.
- Store the spawn list in the manifest (or bake into `_enemyStartPointParams`).
- Enemy count is the win condition. Timer/exit spawns add to it.

### 9.6 Timer

- 3:00 for stages 1–20.
- 2:30 for stages 21–40.
- 2:15 for stages 41–48.
- 2:00 for stages 49–50.
- On 0:00, spawn a wave of Pontans from the exit (or from the field edges if the
  exit is still hidden).
- If the exit is already open, the player can still leave; the Pontans are
  pressure, not a hard fail.

### 9.7 Lives and death

- Start with 3 lives.
- +1 life per stage cleared.
- On death: restart the same stage layout, reset enemies/blocks/items, keep
  permanent Bomb Up / Fire Up, lose temporary items, decrement lives.
- At 0 lives: game over → stage select / retry (casual) or title (arcade).

### 9.8 Progression

- 50 stages in a manifest.
- Stage select unlocked progressively, plus a password/stage code for
  convenience.
- Save: a small sidecar JSON (`og_mode_save.json`) with highest stage, lives,
  permanent stats, score, and settings. Do not touch SBR2's story save.

### 9.9 Sample layout

Generated by `tools/og_mode_gen.py --stage 1`:

```
###############################
#.%.....%.........EE%..%%%.x..#
#.#.#.#E#%#.#.#.#.#.#%#.#.#.#.#
#.....%..%....%.....%%.......%#
#.#.#.#.#.#%#%#.#%#%#.#.#.#.#.#
#..%....%.......%..%...%.%%...#
#.#.#.#.#.#.#.#.#.#.#.#.#%#%#.#
#.%.%......%.......%.%.%..%%.p#
#.#.#.#.#%#.#%#%#.#.#.#.#.#.#.#
#.%......%..%.%.%.%.........%.#
#S.%#.#%#%#.#.#.#.#%#.#.#.#.#.#
#......%%.............E.%...%.#
###############################
```

Legend: `#` hard, `%` soft, `.` floor, `S` start, `E` enemy, `x` hidden exit,
`p` hidden pickup. `x` and `p` are shown here for design clarity; in play they
are under soft blocks.

The hard skeleton is the repeated NES map; the `%`/`E`/`x`/`p` placement is the
per-stage randomization. Stage 1 uses 55 soft blocks and 4 Balloms, matching the
atlas counts.

### 9.10 Difficulty curve

These are the values implemented by `tools/og_mode_gen.py`.

| Stages | Enemy pool | Count | Soft blocks | Timer | Notes |
| --- | --- | ---: | ---: | ---: | --- |
| 1–5 | Ballom | 4 | 55–61 | 3:00 | Teach bombing, exit, item |
| 6–10 | + Onil, Dahl | 5 | 62–68 | 3:00 | First chasers |
| 11–20 | + Minvo | 6 | 70–83 | 3:00 | Axis chasers, real pressure |
| 21–30 | + Doria | 7 | 85–98 | 2:30 | First ghost enemy |
| 31–40 | + Ovape, Pass | 8 | 100–113 | 2:30 | Ghosts + relentless chaser |
| 41–48 | all | 9 | 115–125 | 2:15 | Mixed compositions |
| 49 | all + 1 Pontan | 10 | 127 | 2:00 | Pre-final |
| 50 | all + 2 Pontans | 10 | 129 | 2:00 | Final |

Enemy count is the primary knob. Soft-block count is the secondary knob. Do not
raise both aggressively at once.

---

## 10. Content and data plan

### Stage manifest

```json
{
  "id": 1,
  "seed": 1218345120,
  "width": 31,
  "height": 13,
  "item": "fire_up",
  "time_limit": 180,
  "soft_blocks": 55,
  "exit_cell": [27, 1],
  "pickup_cell": [29, 7],
  "enemies": [
    { "type": "ballom", "x": 7,  "z": 2 },
    { "type": "ballom", "x": 19, "z": 1 },
    { "type": "ballom", "x": 22, "z": 11 },
    { "type": "ballom", "x": 18, "z": 1 }
  ]
}
```

### Map assets — already generated

The v1 route is implemented:

- `tools/og_mode_gen.py` builds the base map, randomizes soft blocks, enemies,
  exit and pickup, and emits the 50-stage manifest.
- `tools/og_mode_mapgen.py` clones `map_data_vs001`, rewrites `_mapSize`,
  `_blockDeployParams`, `_playerStartPointParams` and
  `_enemyStartPointParams`, and writes encrypted bundles to
  `build/og_mode/map/data/og/`.

Each bundle is **31 × 13**, has **611–685 block deploy entries**, and carries
4–10 enemy start points. The generated files are test assets, not installed into
the game.

Current output:

- `build/og_mode/og_mode_stages.json` — 50-stage manifest.
- `build/og_mode/map/data/og/map_data_og001` … `map_data_og050` — encrypted
  MapData bundles with patched internal asset/bundle names.
- `build/og_mode/README.md` — regenerate/verify notes.

The generated bundles keep the template's `_mapResDataParam.id = 100`, so they
reuse the versus environment/block prefabs. If SBR2's versus mode rejects a
31 × 13 arena, the OG Mode plugin needs its own scrolling camera and launch
path; the map data itself is valid.

A runtime layout route (one template + rewriting the grid after load) is still
possible later, but pre-generated bundles are the safer first test.

### Plugin

A BepInEx 6 IL2CPP plugin provides:

- OG Mode entry / stage select,
- manifest loading,
- enemy spawn + AI,
- bomb-blast queries,
- exit door + pickup,
- timer/lives/score HUD,
- stage transitions,
- sidecar save.

Data-only pieces (maps, BGM, text) should work without the plugin where
possible, but the mode logic is inherently code.

---

## 11. Milestones

1. **Research/spec** — this document. **Done.**
2. **Base map + generator** — 31 × 13 skeleton, random soft blocks/enemies,
   ROM power-up table. **Done** (`tools/og_mode_gen.py`).
3. **50 MapData bundles** — encrypted, verified with UnityPy. **Done**
   (`tools/og_mode_mapgen.py`).
4. **Spike** — offline map + one custom enemy + bomb kill + door. **Next.**
5. **Stage 1 runtime** — one pickup, exit, timer, HUD, lives.
6. **Enemy set** — all eight behaviors, including ghost and axis-lock.
7. **50-stage curve** — stage transitions, stage select, save.
8. **Polish** — score, death/retry, item destruction toggle, OG BGM/SFX.
9. **Optional** — bonus stages, secret items, 2P co-op, CRT/pixel presentation.

The critical path is milestone 4. Everything else is incremental.

---

## 12. Open questions

1. **Host mode.** Can a custom offline mode be launched from the SBR2 menu
   without replacing an existing mode? Or is it easier to hijack an offline
   versus battle and overlay the NES logic?
2. **Enemy spawning.** Can SBR2 enemy/minion prefabs be instantiated on a vs map
   outside the story rule? If not, use custom shells.
3. **Bomb blast query.** What is the cleanest hook to ask "which grid cells did
   this explosion cover?" (`BombControllerHost` / `AbstractBomb` events).
4. **Door.** Does SBR2 have a generic goal/door block that can be opened by a
   plugin, or do we spawn our own?
5. **HUD.** Reuse SBR2's battle HUD or build a small custom canvas?
6. **Save.** Sidecar JSON is safest; confirm it survives SBR2 updates.
7. **Co-op.** Offline battle already supports multiple local players; decide
   whether OG Mode is 1P or 1–2P in v1.
8. **Legal.** NES names, sprites, music, and stage layouts are Konami IP. For a
   public release, use original/recreated art and music or require user-owned
   assets. For personal use, this is a non-issue.

---

## 13. Bottom line

- The NES loop is **one long field, one pickup, kill all enemies, find the door,
  don't waste the exit/item/timer**.
- Its genius is **mistake-to-enemy conversion**, not content volume.
- A faithful SBR2 version is a **generated 50-stage arcade mode**, not 50
  hand-built maps and not a story campaign.
- SBR2 already provides the grid, bombs, items, and map format. The real work is
  a custom PvE rule/enemy layer and a small HUD/progression shell.
- This is a much better first "celebration" project than the SBR1 story port:
  smaller, replayable, and every stage is independently playable.

---

## Sources

- Wikipedia, *Bomberman (1985 video game)* — 50 levels, eight enemy types, three
  lives + one per level, timer spawns enemies, exit bombing spawns enemies,
  permanent vs temporary power-ups, bonus stages, password.
  https://en.wikipedia.org/wiki/Bomberman_(1985_video_game)
- Bomberman Wiki, *Bomberman (NES)* — objective, one power-up per stage, secret
  item, exit rules.
  https://bomberman.fandom.com/wiki/Bomberman_(NES)
- Bomberman Wiki, *Bomberman (NES)/Items* — exact NES item list, caps, and
  secret bonus items.
  https://bomberman.fandom.com/wiki/Bomberman_(NES)/Items
- Bomberman Wiki enemy pages — Ballom, Onil, Dahl, Minvo, Doria, Ovape, Pass,
  Pontan: speeds, chase probabilities, soft-block pass, scores, guardian item
  associations.
- VGMaps, *Bomberman Stages* atlas — 29 × 11 interior, 1-tile border,
  31 × 13 total, 2-tile pillar pitch.
  https://vgmaps.com/Atlas/NES/Bomberman-Stages.png
- `Bomberman (USA).nes` (user-provided) — power-up table at PRG offset `0xC04`,
  read as `$CC03,X` (1-indexed by stage). The 50 entries match the NES item
  progression, including Speed Up on stage 4.
- TASVideos, *GameResources/NES/Bomberman* — the `PermutateRandomCtrlVars` RNG
  used by OG Mode's generator.
  https://tasvideos.org/GameResources/NES/Bomberman
- `tools/og_mode_gen.py` — OG Mode base-map, soft-block, enemy, exit, pickup
  and 50-stage manifest generator.
