# Super Bomberman R 2 — criticism → what we can fix

Companion to `docs/SBR2_SCOUT.md`. This is the "what would a community patch
actually change?" pass: player/critic complaints, the data or code behind them,
and what is realistic with the asset/code access we confirmed.

---

## Sources and method

Critic reviews:

- **Game Informer — 5.75/10** ("A Disappointing Dud"): story is the worst part,
  annoying characters/slow voice lines, exploration doesn't fit Bomberman,
  auto-upgrades, losing Ellons/retracing, online mode windows and small
  playerbase, Castle AI exploit.
- **Push Square — 7/10**: repetitive gameplay, visually cluttered (hard to see
  bombs), occasional frame drops, long load times; praises Castle and content.
- **Nintendo Life — 8/10**: great multiplayer, story "kinda meh"/repetitive,
  basic visuals, level editor fiddly.
- **OpenCritic/Metacritic — 68**: mixed/average.
- **GameFAQs mini-review**: repetitive, visual clutter, frame drops, long loads.

Player reviews (Steam negative/positive, Reddit):

- online is dead/bot-filled, rotating modes, no mode choice, ranked mismatch,
  no casual/private lobby;
- only 8 Standard maps, one Battle 64 stage, fewer modes/power-ups/characters,
  no team Standard, no CPU difficulty;
- story is boring/grindy, no co-op, slow cutscenes, Ellons die/retracing;
- AI is terrible, Castle is unbalanced/exploitable, editor is clunky and
  Castle-only;
- price/value, recycled R Online content.

Positive themes worth preserving: core Bomberman gameplay, local multiplayer,
Battle 64, Castle idea, crossplay, lots of unlockables, no microtransactions.

Data/code inspection:

- Decrypted `defines/common` (493 MonoBehaviour data tables).
- Inspected `MapInfoDataTable`, `BattleModeUIDataTable`,
  `StoryCampExpTable_*`, `StoryCharacterStatusDataTable`,
  `EnemyInfoDataTable`, `NpcWeakDefData`/`NpcStrongDefData`,
  `ShopProductTable`, `StageEdit*`, release-condition tables.
- Verified the edit pipeline on a copy: changed
  `StoryCampExpTable_World1._datas[0]._nextLevelNeedExp` from 3000 to 1800,
  saved, re-encrypted the 0x100 header, reloaded — value persisted.
- Verified `BattleModeUIDataTable` list-copy: appended Battle 64 maps
  20001/25001 and Standard map 1 with their existing thumbnails.
- **No game files were modified** during this analysis.

---

## 1. What is actually fixable

### Tier 1 — data-only, high impact, low risk

| Criticism | Fix | Where | Effort |
| --- | --- | --- | --- |
| Story is grindy/repetitive | Cut EXP requirements; buff story starting stats; slow enemies | `StoryCampExpTable_World1/2/3`, `StoryCharacterStatusDataTable`, `EnemyInfoDataTable` | **Easy** |
| Story is frustrating (Ellons die, retracing, penalties) | Remove/soften EXP penalties on defeat/game over; protect Ellons; add fast travel/checkpoints | `StrBattleDefData`, story MonoBehaviours, code | **Medium** (data + code) |
| Only 8 Standard maps | Expose existing 8P/16P map layouts: `MapInfoDataTable` has **24** Standard maps, `BattleModeUIDataTable` exposes only 8 | `BattleModeUIDataTable` (copy entries), `MapInfoDataTable` | **Easy** (data), UI test needed |
| Only one Battle 64 stage | Expose both map IDs **20001** and **25001** | `BattleModeUIDataTable` | **Easy** |
| Few Crystal/Basic/Grand Prix maps | Expose map **10000** (Basic) and **10001** (Crystal); 4 maps exist | `BattleModeUIDataTable` | **Easy** |
| AI is terrible / bots suicide | Tune `NpcWeakDefData`/`NpcStrongDefData` (danger avoidance, bomb vision, bomb rate, search distance) | `defines/common` | **Easy-Medium** |
| Castle unbalanced / attackers stop | Rebalance key supply, shutter, respawn, supply-block timings | `CstlBattleDefData` | **Easy-Medium** |
| Level editor limits | Raise part counts; unlock `_isUsableRegulaMode`; unlock parts | `StageEditPartsDataTable`, `StageEditPartsReleaseConditionDataTable` | **Easy-Medium** |
| Characters/costumes/icons locked | Unlock release conditions; 38 characters, 63 costumes, 97 accessories, 96 icons | `*ReleaseConditionDataTable` | **Easy** (test save gating) |
| Shop prices/grind | Scale in-game coin prices (205 products; 26 at 1000) | `ShopProductTable` | **Easy** (optional) |
| No CPU difficulty | Expose existing COM strength (`NETWORK_READY_RULE_COM_STRENGTH`) and map it to weak/strong AI profiles | code/UI + data | **Medium** |
| No item/rule toggles | The game already has rule keys: skull, Louie, special skill, outside attack, pressure block, revenge cart, set count, start position | `NET_STD_BATTLE_RULE_*` code/UI | **Medium** |
| No team Standard | Team-select code exists (`NETWORK_READY_TEAMSELECT`), but Standard team rules may not; needs code investigation | code | **Medium-Hard** |
| No quick rematch | `NET_BATTLE_COMMON_PLAY_AGAIN` already exists; expose it offline / skip result flow | code/UI | **Medium** |
| Bad English voice/cutscenes | Japanese voice packs are included (`sounds/voice/character_j`, `story_j`); `VOICE_LANGUAGE` setting exists | data/config/UI | **Easy** |
| Visual clutter / frame drops | Disable/reduce bloom, DoF, motion blur, vignette, grain, chromatic aberration; lower particles/shake; add outlines | BepInEx 6 + HDRP volume components | **Medium** |
| Long loads/menu lag | Skip logos/intro; reduce menu animation; preload/cache; lower shader variants | code/UI/asset | **Medium** |
| Ultrawide/FOV/limited resolutions | Camera aspect/FOV patch, borderless fullscreen | code | **Medium** |
| Switch-level visuals | PC texture upscales, sharper UI, HDRP lighting tweaks | asset mods | **Easy-Medium** |

### Tier 2 — code/UI mods, medium effort

- **Expose existing rule toggles** in offline/private rooms. The localization
  keys show the rules already exist:
  `NET_STD_BATTLE_RULE_ENABLE_SKULL_*`, `ENABLE_SPECIAL_SKILL_*`,
  `ENABLE_OUTSIDE_ATTACK_*`, `PRESSURE_BLOCK_*`, `REVENGE_CART_*`,
  `SET_COUNT_*`, `BATTLE_START_POSITION_*`; Castle/Battle 64/Grand Prix have
  their own rule sets.
- **COM strength / team select**: `NETWORK_READY_RULE_COM_STRENGTH` and
  `NETWORK_READY_TEAMSELECT_*` exist; expose them in offline/private lobbies.
- **Castle fixes**: end the "attackers stop after a chest" behavior, improve
  defender AI, add anti-cheese checks for unreachable bases.
- **Editor improvements**: Standard-mode templates (duplicate the 40 Castle
  templates with `_battleMode = 0` and VS map data), better controls, higher
  part limits.
- **Performance/QoL plugin**: logo/cutscene skip, post-processing toggles,
  FOV/ultrawide, faster menus, play-again, Japanese voices.
- **Japanese voice switch**: the `_j` packs are on disk; the options key is
  `OTHER_BASIC_VOICE_LANGUAGE` / `VOICE_LANGUAGE`.

### Tier 3 — hard or not fixable

| Item | Reality |
| --- | --- |
| Online population / "dead online" | Cannot be fixed by a mod. Best mitigations: smarter bots, bot backfill, private lobbies, community events. |
| Rotating ranked modes | Server-driven. A client lobby browser may help, but cannot force the official rotation. |
| Ranked mismatch (newbies vs veterans) | Server/matchmaking; not fixable client-side. |
| Story co-op | Hard: second player, split-screen, camera, save/progression, UI. |
| Missing Grand Prix modes (Checkpoints/Civilians) | Content/code not present; not a data flip. |
| New characters/power-ups/stages from scratch | Possible as custom assets, but large scope; only unlock existing content cheaply. |
| Price / marketing / core content scope | Store/publisher side. |
| Switch performance/visuals | Only relevant on Switch; PC mods don't fix console builds. |
| Online cheating/integrity | Do not build; crossplay and ToS risk. |

---

## 2. Concrete data evidence

From `defines/common`:

| Table | Finding |
| --- | --- |
| `StoryCampExpTable_World1/2/3` | 14 levels each; **144,000 EXP per world**, max 20,000 per level |
| `StoryCharacterStatusDataTable` | 38 entries; starting speed/fire/bombs/kick/punch/throw |
| `EnemyInfoDataTable` | 13 enemy types; `_life`, `_moveSpeed`, search distances |
| `NpcWeakDefData` / `NpcStrongDefData` | AI weights; strong profile has `_moveRateOverDanger = 0.01`, `_bombVisionScore = -200` |
| `MapInfoDataTable` | **43 maps**: 24 Standard, 4 Crystal/Basic, 2 Battle 64, 13 Castle |
| `BattleModeUIDataTable` | Exposes **8 Standard** maps + random; Crystal 10002/10003 + random; Basic, Battle 64 and Castle are **random-only** |
| `BattleModeUIDataTable` modes | 0 Standard, 1 Grand Prix/Crystal, 2 Grand Prix/Basic, 3 Battle 64, 4 Castle |
| `StageEditTemplateMapDataTable` | 40 templates, **all `_battleMode = 3` (Castle)** |
| `StageEditPartsDataTable` | 20 part types; per-mode count limits and `_isUsableRegulaMode` |
| `ShopProductTable` | 205 products; prices mostly 100/300, 26 at 1000 |
| `CharacterUIDataTable` | 38 playable characters |
| `CostumeReleaseConditionDataTable` | 63 costumes |
| `AccessoryReleaseConditionDataTable` | 97 accessories |
| `PlayerIconReleaseConditionDataTable` | 96 icons |
| `StageEditPartsReleaseConditionDataTable` | 19 editor parts |
| Voice bundles | `sounds/voice/character_e`, `character_j`, `story_e`, `story_j` — both languages included |

Rule/UI keys found in metadata:

- `NET_STD_BATTLE_RULE_*`, `NET_CSTL_BATTLE_RULE_*`,
  `NET_GP_BATTLE_RULE_*`, `NET_BR_BATTLE_RULE_*`
- `NETWORK_READY_RULE_COM_STRENGTH`, `NETWORK_READY_TEAMSELECT_*`
- `NET_BATTLE_COMMON_PLAY_AGAIN`
- `ROOM_TAG_STANDARD`, `ROOM_TAG_BATTLE64`, `ROOM_TAG_CASTLE`,
  `ROOM_TAG_GRANDPRIX`, `ROOM_TAG_PRACTICE`, plus beginner/veteran/party tags
- `OTHER_BASIC_VOICE_LANGUAGE`, `OTHER_QUALITY_*`, `STORY_EXPLORE_PUZZLE_DIFFICULTY`
- `STORY_READY_COMMON_MAP_PROTECTED_ELON` / `UNPROTECTED_ELON`
- `STORY_READY_CAMP_EXP_RESULT_PENALTY_DEFEAT` / `GAME_OVER`

---

## 3. Proposed "community patch v1"

This is deliberately conservative: data edits that are easy to test, plus a
BepInEx 6 QoL plugin. Online changes stay out of v1.

> **Safety:** data patches are global game-data edits (EXP, AI, shop prices).
> Use them offline/private, keep the `.orig` backup, and restore with Steam
> "Verify integrity" before playing online. Do not bring gameplay/economy
> patches into crossplay or ranked.

### 3.1 Data profile

`patches/sbr2-qol-v1.json` applies:

- Story EXP ×0.6 in all three worlds (144k → 86.4k per world; 40% less grind).
- Story starting fire power +1 and bomb count +1.
- Enemy move speed ×0.8.
- Strong NPC AI: danger avoidance `0.01 → 0.35`, bomb vision
  `-200 → 100`, bomb rate `0.7 → 0.85`.
- Shop prices ×0.5 (in-game coins; optional/cosmetic).

Apply with:

```sh
python3 tools/sbr2_data_patch.py \
  --patch patches/sbr2-qol-v1.json --dry-run

python3 tools/sbr2_data_patch.py \
  --patch patches/sbr2-qol-v1.json \
  --out /var/home/user/.cache/sbr2-patched
```

`--in-place` is supported and keeps `defines/common.orig`.

### 3.2 QoL plugin (BepInEx 6 x64 IL2CPP)

Port the SBR 1 BomberQoL feature set:

- skip publisher logos and cutscenes (`SplashLogo`, `EntryScene.LogoManage`,
  `MovieManager`, `VideoPlayer`);
- Japanese voice switch (`VOICE_LANGUAGE`);
- post-processing toggles (Bloom, DepthOfField, MotionBlur, Vignette,
  FilmGrain, ChromaticAberration, LensDistortion);
- FOV / ultrawide / borderless;
- faster menus and a `Play Again` shortcut;
- expose COM strength / existing rule toggles in offline/private rooms.

### 3.3 Experimental v2

- Expand `BattleModeUIDataTable` with the unused 8P/16P Standard layouts,
  Battle 64 maps 20001/25001, Crystal 10001, Basic 10000, and more Castle
  presets. Copy existing entries to keep thumbnails/banners. **UI test needed.**
- Raise editor part limits and unlock parts; add Standard-mode templates.
- Castle balance (`CstlBattleDefData`).
- Lobby browser / bot backfill (last, online risk).

---

## 4. What we should not promise

- A fix for price, population, marketing, or "the game feels recycled."
- A client-side fix for official ranked rotation or ranked matchmaking.
- New Grand Prix modes or a full story co-op mode in a first patch.
- Online-safe gameplay mods: crossplay means desync/ToS risk.

---

## 5. Recommended order

1. **Ship the story/AI/shop data profile** (`patches/sbr2-qol-v1.json`) after an
   in-game smoke test on a copied `defines/common`.
2. **Port the QoL plugin** (skip logos/cutscenes, voice language, post-processing,
   FOV/ultrawide, play-again).
3. **Expose existing rule toggles** and COM strength.
4. **Map/mode expansion** (data copy) with UI testing.
5. **Castle/editor balance**.
6. **Lobby/bot backfill** only if the online APIs behave and the community wants it.

The first two steps fix the most-complained-about parts of the game without
touching online or adding new content.
