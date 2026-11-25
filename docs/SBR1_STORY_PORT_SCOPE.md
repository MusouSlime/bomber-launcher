# Porting SBR1 story mode into the SBR2 engine — full scope

Status: scoping only. No port work started.

Short version:

- **Full 1:1 SBR1 campaign in SBR2:** low feasibility for a solo/small mod
  project; roughly a multi-year fan-remake effort.
- **One-world vertical slice:** plausible but still months, not weeks.
- **Single-stage proof of concept:** reasonable first test.
- **Systems-only port** (linear stage flow/stock/gems/world map) without all 62
  stages: hard but smaller than the full content port.
- **Audio/text/character assets:** the portable part; maps, bosses, co-op, UI
  and progression are the expensive parts.

---

## 1. Actual SBR1 campaign size

From `common_pack` / `StoryMapInfoDataTable_world1..7`:

| World | Story stages | Boss stages | Flow nodes |
| --- | ---: | ---: | ---: |
| 1 | 10 | 2 | 17 |
| 2 | 10 | 2 | 17 |
| 3 | 10 | 2 | 17 |
| 4 | 10 | 2 | 17 |
| 5 | 10 | 2 | 17 |
| 6 | 2 | 2 | 9 |
| 7 | 10 | 1 | 17 |
| **Total** | **62** | **13** | **111** |

Also:

- **7 worlds** (`WorldInfoDataTable_main`, `StoryListDataTable`).
- **4 difficulty tables**: Easy, Normal, Hard, Super (`StoryInfoDataTable_*`).
- **2-player story co-op** (`StoryBattleScene.SECOND_PLAYER_STATE`).
- Separate scene flow: `WorldMap`, `StageSetup`, `StoryBattle`, `WorldResult`,
  `MoviePlay`, `SaveDataSelect`.
- Story systems: stock/lives, gems, continues, stage goals, boss robots,
  world scores, rewards.
- 62 story map packs (`map/data/map_data_pack_worldXX_YY`).

SBR2 for comparison:

- **3 open worlds**, **5 bosses**.
- `StoryExploreScene` / `StoryExploreLogic` / `StoryCampScene` /
  `StoryCampLogic`.
- Single-player story; no co-op.
- Open-area exploration, Ellons, camp leveling, Castle/base-defence side
  events.
- 90 `map/data/str` files, 126 `map/env/str`, 81 `map/level` files.

The mismatch is not just content volume; it is a different story architecture.

---

## 2. What “port into SBR2 engine” can mean

### Option A — 1:1 remake inside SBR2

Recreate SBR1’s worlds, 62 stages, 13 bosses, cutscenes, UI, stage select,
stock/gem/continue rules, difficulty tables, co-op and save progression inside
SBR2.

This is effectively a **total-conversion fan remake**. You cannot import the
SBR1 scenes/scripts directly because:

- SBR1 is Unity 5.6 Mono; SBR2 is Unity 2020 IL2CPP/HDRP.
- SBR1 story is a dedicated `StoryBattleScene` + `WorldMap` + `StageSetup`
  system; SBR2 story is `StoryExplore` + camp + open areas.
- SBR1 is 2-player co-op; SBR2 story is single-player.
- Map assets, enemy/boss scripts, UI prefabs, materials and shaders differ.

Feasibility: **~2/10**.

### Option B — SBR1 campaign adapted to SBR2 systems

Keep the SBR1 narrative/world order, but rebuild it using SBR2’s explore/battle
systems:

- Recreate SBR1 stage layouts as SBR2 `MapData`/`MapEnv` maps.
- Use SBR2 enemies and boss robots first; port specific SBR1 bosses later.
- Use SBR2’s story flow/camp or a custom mission-select screen.
- Drop or redesign co-op, stock/gems/continues.

This is the more realistic “port,” but it is a **new campaign** using SBR1 as a
design source, not a 1:1 port.

Feasibility: **~5/10 for one world; ~2/10 for all seven**.

### Option C — Systems-only port

Recreate SBR1’s **linear story structure** in SBR2:

- World map / stage select.
- Stage setup + difficulty.
- Stock, gems, continue, score.
- Story battle HUD and results.

Reuse SBR2 battle/explore content. No 62-stage content port.

Feasibility: **~4/10**; still months of UI/save/scene work.

### Option D — Asset/design extraction only

Port music, voice, text, character models, stage layouts as reference and
rebuild a small number of favorite stages as SBR2 custom content.

Feasibility: **~7/10** for a small pack; this is the sane first step.

### Option E — Port SBR1 story to an SBR1 source port instead

Decompile SBR1’s Mono `Assembly-CSharp.dll`, rebuild the original linear story
in a Unity 5.6/modern Unity project, then add QoL/characters. This preserves the
original design and co-op far better than grafting it into SBR2.

Feasibility: **~4/10** overall, but much more coherent than Option A.

---

## 3. Component-by-component feasibility

| Component | Reuse from SBR1? | Effort | Notes |
| --- | --- | --- | --- |
| BGM / SFX / voice | Yes, with conversion | Low | External audio works; SBR1 packs decrypt with our tools |
| Story text / dialogue | Mostly | Low–Medium | Localization tables; SBR2 UI uses its own keys |
| Cutscene videos | Yes | Medium | SBR1 movies are MP4; SBR2 has `MoviePlayUI`; file size/localization |
| Character models | Partial | Medium | Rigs match; materials/shaders need conversion |
| Map geometry | Partially | High | Both use grid `MapData`, but semantics/scale/visuals differ; 62 maps |
| Map environments / lighting | No | High | SBR1 legacy vs SBR2 HDRP/Fuse |
| Enemies | Data/behavior reference | High | Reimplement or substitute SBR2 enemies |
| Boss robots | Data/behavior reference | Very high | SBR1 `BossRobotController` vs SBR2 boss system; 13 bosses |
| Story flow | Concept only | High | SBR1 `StoryFlowDataTable` schema differs; linear vs open-world |
| Stage select / world map UI | No | High | Rebuild `WorldMap`/`StageSetup`/`WorldResult` |
| Story battle HUD | No | High | Stock/gems/continue/score vs SBR2 camp/explore HUD |
| Co-op | Yes in SBR1 | Very high | SBR2 story is single-player; adding co-op is a separate project |
| Save/progression | No | High | SBR1 StoryData vs SBR2 SaveStoryData; new save slot/mode |
| Difficulty tables | Data reference | Medium | 4 SBR1 difficulty tables; SBR2 uses camp level/difficulty differently |
| Legal/distribution | No | High | SBR1 assets cannot be shipped; installer must extract from owned copy |

---

## 4. Rough effort estimates

Assumes one experienced modder, existing asset/crypto tools, reusing SBR2
enemies/bosses/UI where possible, no co-op.

| Milestone | Rough effort |
| --- | --- |
| One SBR1 stage rebuilt in SBR2 (geometry, enemies, goal, placeholder UI) | 2–6 weeks |
| One stage + one SBR1 boss + proper HUD/result | 1–3 months |
| **World 1 vertical slice** (10 stages, 2 bosses, world/stage UI, save, cutscenes) | **4–8 months** |
| Systems-only linear story framework | 3–6 months |
| All 7 worlds, adapted content | 2–3 years |
| 1:1 including co-op, exact bosses, all cutscenes, stock/gems/continue | 3–5+ years / small team |

These are order-of-magnitude estimates, not promises. The first stage is the
most important measurement: if a single map conversion takes weeks, 62 stages
is not viable.

---

## 5. The biggest blockers

1. **Architecture mismatch.** SBR1 is a linear `StoryBattleScene` + `WorldMap` +
   `StageSetup`; SBR2 is open-world `StoryExplore` + camp. You are not dropping
   stages into the same flow.
2. **Co-op.** SBR1 story is 2-player; SBR2 story is single-player. Adding local
   co-op to SBR2 story means second player, camera/split-screen, HUD, save,
   progression and boss logic.
3. **Maps.** 62 stage maps plus 13 boss maps; SBR1 Unity 5.6 legacy vs SBR2
   HDRP `MapData`/`MapEnv`/`MapLevel`. Basic grid conversion is possible;
   visuals/gameplay semantics are not.
4. **Bosses/enemies.** SBR1 boss robot controllers/AI are a different system
   from SBR2’s boss framework; 13 bosses is a lot of behavior work.
5. **UI/progression.** Stage select, world map, stock/gems/continue, difficulty,
   results, save; all need rebuilding.
6. **Legal.** SBR1 assets are Konami’s; a distributed mod cannot include them.
   An installer would have to extract from the user’s own SBR1 copy, adding
   complexity and distribution friction.

---

## 6. Recommended path

### Do not start with “port all 62 stages”

Start with a **single-stage proof of concept**:

1. Pick SBR1 World 1 Stage 1.
2. Rebuild its grid/geometry in SBR2 `MapData` (or a custom stage-editor map).
3. Use one SBR2 enemy type and SBR2 player.
4. Implement the SBR1 goal (annihilate enemies / switch / boss).
5. Add a minimal result screen and a way to launch it.
6. Measure:
   - hours per map;
   - enemy/boss gap;
   - UI/save gap;
   - visual/material conversion cost.

### Decision gate

- If one stage takes **< 1 week** and feels good, build a **World 1 vertical
  slice**.
- If one stage takes **weeks**, stop and choose:
  - **SBR1 source port** (preserve original story/co-op), or
  - **original SBR2 mini-campaigns** (use SBR2 systems rather than port SBR1).

### Best long-term options

| Goal | Best route |
| --- | --- |
| Play SBR1 story with QoL/characters now | SBR1 + BepInEx 5 (already installed) |
| Play SBR1 story with SBR2 visuals/engine | Multi-year fan remake; vertical slice first |
| New story content in SBR2 | Build original SBR2 campaigns/maps, not a 1:1 SBR1 port |
| Preserve SBR1 story for other platforms | SBR1 source port/recomp, not SBR2 |

---

## 7. Bottom line

- **Full 1:1 SBR1 story in SBR2:** technically possible, practically **not
  feasible** for a solo/small project. Treat it as a fan remake, not a mod.
- **One world / vertical slice:** feasible but months of work; worth doing only
  after a single-stage test.
- **Assets/audio/text/characters:** the reusable 20%; port these first.
- **Maps, bosses, co-op, UI, save:** the other 80%; they define the project.

The sane sequence is: play casually → finish the SBR2 QoL/custom-content plugin
→ build one SBR1 stage as a test → decide between a World 1 adaptation and an
SBR1 source port. Do not commit to all seven worlds before that test.
