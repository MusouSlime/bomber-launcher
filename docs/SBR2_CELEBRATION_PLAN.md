# SBR2 as a Bomberman celebration — stage/item/mechanics plan

Status: scoping/planning. No content built yet.

Short answer: **yes, and it is a much better fit for SBR2 than porting the SBR1
story.** The celebration is additive battle content: classic stages, classic
rules/items, retro presentation and selected mechanics. It reuses SBR2’s
existing battle/map/item framework instead of rebuilding a whole campaign.

The realistic product is a **“Bomberman Museum” mode**:

- stage select grouped by game (`Bomberman '93`, `Super Bomberman 1–5`,
  `Saturn Bomberman`, etc.);
- classic rule presets;
- classic item sets/visuals;
- classic BGM per stage;
- retro UI/announcer;
- selected older gimmicks and modes added incrementally.

---

## 1. Why this is feasible

SBR2 already has the right foundation:

- grid-based battle maps with `MapData` / `MapEnv` / `MapLevel`;
- Standard, Grand Prix/Crystal, Battle 64 and Castle modes;
- 43 maps in `MapInfoDataTable`, 2,705 asset bundles;
- 20 item types in `ItemInfoDataTable_G5`;
- map gimmicks/parts in `StageEditPartsDataTable`;
- character rigs shared with SBR1, so classic character skins are viable;
- per-stage BGM and a custom BGM folder;
- rule toggles already present (`NET_STD_BATTLE_RULE_*`): skull, Louie, special
  skills, outside attack, pressure block, revenge cart, set count, start
  position.

SBR1’s item list is a useful baseline:

```
FIRE_UP, FIRE_DOWN, FIRE_FULL, BOMB_UP, BOMB_DOWN,
SPEED_UP, SPEED_DOWN, PUNCH, POWER_GLOVE, KICK, REMOTE,
PENETRATION, SKULL, ELASTICITY, KEY, CRYSTAL
```

SBR2 already covers most of those, plus hearts, eggs, keys, coins/jewels and
bomb types. The gaps are specific classic items (notably remote control and
some older bomb/item variants), not the whole item system.

---

## 2. What to build

### A. Classic Stage Pack (highest value, lowest engine risk)

Recreate classic 2D stage layouts as SBR2 battle maps.

- 13×11-ish Bomberman grid; SBR2 maps are 3D but gameplay is the same grid.
- Block layout, spawn points, item blocks, warp/exit tiles.
- Visual theme per game: either SBR2-style remakes or retro-style original art.
- Add entries to `MapInfoDataTable` and `BattleModeUIDataTable`; provide
  thumbnails/banners and a stage-select grouping.
- Per-stage BGM (classic track or a custom replacement).
- Use Standard mode first; custom rule preset second.

Candidate sets:

- `Bomberman '93` / `Super Bomberman 1` — simple, iconic layouts.
- `Super Bomberman 2–5` — gimmicks, bosses, battle stages.
- `Saturn Bomberman` — 8/10-player layouts.
- `Bomberman Live/Ultra` — modern-classic layouts.

Avoid 3D `Bomberman 64`/`Second Attack` stages in v1; different geometry,
camera and rules.

Feasibility: **8/10**. The work is content authoring, not engine work.

### B. Classic Rule Presets

Add “Classic”, “Super Bomberman”, “Saturn”, “Battle 64” style rule presets:

- item set on/off;
- skull on/off;
- Louie on/off;
- special skills off;
- pressure block / revenge cart options;
- start position fixed/random;
- set count / time limit;
- classic sudden-death behavior.

Much of this already exists in the code/localization; expose it in a museum
menu and store it per stage.

Feasibility: **8/10**.

### C. Classic Item Pack

Two layers:

1. **Reskin/reuse existing SBR2 item types** as classic items:
   fire/bomb/speed, kick, punch, power glove, penetration, rubber/elasticity,
   skull, heart, key.
2. **Add missing classic behaviors**:
   - remote control bomb;
   - power bomb / bomb types;
   - shield/vest;
   - clock/time stop;
   - mine;
   - magnet;
   - spike/pierce variants;
   - curse/skull variants;
   - 1-up / score items.

Layer 1 is data/asset work. Layer 2 needs a BepInEx 6 code mod: new item enum
values, prefabs, pickup logic, HUD icons, and rule integration.

Feasibility: **8/10 for reskins; ~5/10 for new behaviors**.

### D. Classic Gimmicks

Audit what SBR2’s map parts already support before writing code. Likely
candidates:

- conveyor belts / treadmills;
- warp holes / pipes;
- switches and gates;
- slime/ice floors;
- cannons/lasers/springs;
- mine carts / moving platforms;
- falling blocks / pressure blocks;
- revenge cart (already present);
- Louie mounts (partially present).

Reuse existing `StageEditPartsDataTable`/map objects where possible; add code
only for missing gimmicks.

Feasibility: **6/10**; some are map-data work, some are code.

### E. Classic Modes

Older modes are the least reusable:

- Reversi Battle (paint tiles);
- Hyper Bomber (collect orbs/gate);
- Countdown/Tag;
- Dodge Battle;
- Bomberman 64-style battle.

These require custom rule controllers, HUD, win conditions and map logic.

Feasibility: **3/10** each; worth doing only after stages/items/presentation.

### F. Retro Presentation

- Museum stage-select UI grouped by game/year.
- Pixel fonts, CRT/scanline option, classic announcer.
- Classic stage intro cards.
- Per-stage classic BGM.
- Character skins/costumes from older games.

Feasibility: **7–8/10**.

---

## 3. Technical architecture

### Content manifest

A museum pack should be declarative:

```
museum/
  stages/
    sb1_03/
      map/          SBR2 MapData/MapEnv/MapLevel bundles
      stage.json    name, origin game, players, rules, BGM, items
      thumb.png
  items/
    remote/         prefab, icon, behavior id
  ui/
    theme/
  music/
```

### Data entries

- `MapInfoDataTable` — map ID, player counts, map/env/level bundles.
- `BattleModeUIDataTable` — stage-select entry, thumbnails, banners.
- `ItemInfoDataTable_G5` — item prefab/icon mapping.
- `CharacterStatusDataTable` / `CharacterUIDataTable` — character skins.
- BGM labels — one classic track per stage.

We already have `tools/sbr2_data_patch.py` for table edits and
`tools/sbr2_pack.py` for decrypt/encrypt/repack.

### Code plugin

BepInEx 6 IL2CPP plugin for:

- museum stage-select screen / grouping;
- classic rule presets;
- new item behaviors and bomb types;
- custom modes/gimmicks;
- per-stage BGM overrides (can reuse the custom BGM plugin);
- HUD/announcer/UI theme.

Keep code mods optional: the stage pack should work with data/asset patches
alone.

### Map authoring

- Use SBR2 `MapData`/`MapEnv`/`MapLevel` bundles, not the Castle-only stage
  editor, for battle maps.
- Build a small converter/authoring tool from a simple 2D grid format
  (`#`, `.`, `S`, item blocks, warp, etc.) to SBR2 `MapData`.
- Reuse SBR2 environment kits/themes for v1; replace with retro/classic themes
  later.

Feasibility of the converter: medium; a 2D grid → SBR2 MapData bridge is much
easier than SBR1 scene conversion.

---

## 4. Scope and effort

| Scope | Feasibility | Rough effort |
| --- | ---: | --- |
| Museum shell + 8 classic stages + BGM | 8/10 | 2–4 months |
| 30–50 classic stages | 7/10 | 1–2 years |
| Classic rule presets | 8/10 | weeks |
| Classic item reskins | 8/10 | weeks–months |
| New item behaviors (remote/power bomb/shield/clock) | 5/10 | months |
| Gimmick pack (conveyors/warps/switches) | 6/10 | months |
| One custom classic mode (Reversi/Countdown) | 3/10 | months |
| Full “celebration” total conversion | 5/10 | multi-year, incremental |

Unlike the SBR1 story port, this scales: each stage/item is independently
shippable, and the museum shell makes even a small pack feel like a product.

---

## 5. Recommended build order

0. **OG Mode** — the 1985 loop as a generated 50-stage arcade mode. This is a
   compact custom mode, not a museum content pack; spec and research are in
   [`SBR2_NES_MODE.md`](SBR2_NES_MODE.md). It is the best first project because
   it reuses SBR2's player/bombs/maps and needs little authored content.
1. **Museum shell** — stage select grouped by game, rule presets, BGM.
2. **Stage Pack 1** — 8 stages: `'93`, `Super Bomberman 1`, `Super Bomberman 2`.
3. **Item Pack 1** — reskin existing SBR2 items to classic icons/names; add
   classic item toggles.
4. **Stage Pack 2** — `Super Bomberman 3–5`, `Saturn Bomberman`.
5. **Item Pack 2** — remote, power bomb, shield, clock, mine, magnet.
6. **Gimmick Pack** — conveyors, warps, switches, gates, ice/slime.
7. **Mode Pack** — Reversi/Countdown/Hyper, one at a time.
8. **Presentation pass** — retro UI/announcer/CRT/character skins.

Do not start with the heavy custom modes (Reversi/Countdown/Hyper). Stages +
rules + items + music deliver the “celebration” feeling much sooner and are far
more reusable. The NES Mode is the exception: it is a small, self-contained
PvE mode with a clear first milestone, not a full rules-engine project.

---

## 6. Caveats

- **Legal:** older Bomberman stages, sprites, music and names are Konami IP.
  For a public release, recreate layouts/art or require user-owned copies; do
  not ship ripped assets. For personal use, the risk is yours.
- **Online:** custom maps/items should stay offline/private; official
  matchmaking will not know them.
- **Map authoring is the real cost.** The engine can do it; 30–50 stages is a
  content project.
- **Custom modes are the least reusable.** Treat them as separate mini-projects.
- **3D classics** (`Bomberman 64`) are a different geometry/camera problem;
  keep the museum 2D-classic at first.

## Bottom line

**Yes.** SBR2 can absolutely become a Bomberman museum/celebration. The
high-feasibility core is classic stages + rule presets + classic item visuals +
music + retro presentation. New mechanics and modes are additive code projects
on top. It is a much better use of the SBR2 engine than porting the SBR1
campaign, because every piece is independently playable and shippable.
