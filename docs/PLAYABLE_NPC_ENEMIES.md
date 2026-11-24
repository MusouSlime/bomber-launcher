# Playable NPCs and enemies — scope

This document covers adding story NPCs and enemy models to the SBR1 character
select as playable characters. It is based on the **PC 2.2** assets, not the
Switch NSPs.

The short version:

- **There is one dedicated story NPC model**: `model_np001_boy`.
- **Most "NPCs" are already playable Bombers** — Magnet, Golem, Phantom, Sexy
  and Plasma are both story bosses and selectable characters.
- **Only one enemy family has a full player-compatible skeleton**: the
  `en001_hige` family.
- The realistic first targets are **Boy** and **Hige**. Balloom and the rest
  need a per-enemy animation adapter.
- Boss robots are out of scope.

## What is actually in the game

### Playable character table

`common_pack` contains:

| Table | Rows | Notes |
|---|---:|---|
| `CharacterInfoDataTable_builtin` | 51 | The full normal roster |
| `CharacterInfoDataTable_dlc` | 6 | Octopus, Golden Vic Viper, kirakira P Bombers |

Every playable row has a `charKey`, a byte `charId` (0–50 used), a
`charPack`, a `charPrefab`, status IDs, icons and a save-reference key.

The table has **no rows for NPCs or enemies**. A new playable entry therefore
needs either a runtime row injection or a table edit, not just a usability
flag.

### Dedicated NPC models

Only one root prefab in the four `chara_pack_*` bundles is a non-player,
non-boss NPC:

| Prefab | Pack | Components | Rig | Clips |
|---|---|---|---|---|
| `model_np001_boy` | `chara/chara_pack_01` | Transform, Animator, AnimEventReceiver, EscortTargetComponent | All 18 player core bones, **identical rest pose** | `boy_help` |

The Boy rig is the best possible starting point:

- Same core bone hierarchy and the same local rest transforms as the player.
- No `bom_pos` and no `knot_root`/topknot bones; these are easy to add as empty
  transforms.
- Two skinned meshes (body and a metallic part) plus face objects.
- It has its own AnimatorController, so its Animator must be replaced with a
  player controller for player movement/bomb/death animations.

Other story characters reuse player models or the five boss Bombers. There is
no hidden second NPC model in the PC 2.2 packs.

### Boss NPCs are already playable

`define_boss_npc_pack` lists the boss NPCs:

```text
magnet, golem, phantom, sexy, plasma
```

These are the same characters that are already selectable. The boss versions
add AI and equipment (`BossNPCEquipmentItemDataTable`: speed/fire/bomb items,
kick, punch, throw, full fire, bomb type), but no new model. "Play as boss
Magnet" is therefore an equipment/status feature, not a new model.

### Enemy rig inventory

Scanned all 20 enemy bundles in `InAppAssets/steam/enemy`:

| Enemy bundle | Player core bones | Clips | Player-compatible? |
|---|---:|---:|---|
| `en001_hige` | **18/18** | 3 | **Yes** — full humanoid rig, `bom_pos` present |
| `en002_blln` and Balloom variants | 8/18 | 3–6 | No — partial body rig |
| `en003_jump` | 9/18 | 5 | No |
| `en004_bomb` | 4/18 | 4 | No |
| `en005_drll` | 0/18 | 6 | No |
| `en006_eatr` | 0/18 | 4 | No |
| `en007_armr` | 4/18 | 5 | No |
| `en008_slim` | 2/18 | 4 | No |
| `en009_magc` | 1/18 | 5 | No |
| `en010_shot` | 4/18 | 6 | No |

The 18 core bones are:

```text
C_hip, C_waist, C_spine, C_head,
L/R_shoulder, L/R_elbow, L/R_hand,
L/R_leg, L/R_knee, L/R_foot, L/R_toe
```

`en001_hige` has four variants (`en001`, `en101_avoid`, `en201_follow`,
`en301_clever`), the full skeleton, `bom_pos`, and idle/walk/death clips. It
also carries `EnemyComponent`, which must be disabled or removed when it is
used as a player.

The Hige rest pose is shorter than the player rest pose, so player animations
will drive it at Hige proportions. That is usually acceptable for a chibi
character but needs visual tuning (root scale/offset).

## Feasibility tiers

### Tier A — Boy NPC (recommended first)

**Effort:** 2–4 days for a working prototype.

Why first:

- Unique NPC, immediately visible as a new character.
- Skeleton and rest pose match the player exactly; player animations should
  transfer without retargeting.
- Only one animation of its own, so there is no complex state machine to
  preserve.

Work:

1. Add a proxy `CharacterInfoData` row (`charKey = npc_boy`, next free byte ID).
2. Point it at a host player prefab and swap/attach the Boy model at
   `Player.CharacterModel.create`.
3. Add empty `bom_pos` and `knot_root` transforms (or disable topknots for this
   character).
4. Use the host player's AnimatorOverrideController.
5. Use the Boy's own face/body meshes and materials.
6. Fall back to a player icon until a Boy icon is rendered.

### Tier B — Hige enemy (recommended second)

**Effort:** 3–6 days after the Boy pipeline exists.

Why second:

- It is the only enemy with the complete player rig.
- It is a recognisable enemy and visually distinct from every Bomber.
- It already has a `bom_pos`, so bomb placement is correct by construction.

Work:

1. Reuse the Tier A proxy-row and model-attach path.
2. Disable `EnemyComponent` and the enemy AI; add the player component set.
3. Assign a player AnimatorOverrideController.
4. Normalize height/scale and the ground offset.
5. Hide or ignore topknot/accessory systems that assume `knot_root`.
6. Test bomb lift/throw, death, special and item pickup animations.

### Tier C — Balloom family and other partial-rig enemies

**Effort:** 1–2 weeks for a reusable adapter, plus per-enemy tuning.

These have only a subset of the player bones. Player walk/death clips cannot
drive them directly. Options:

- Build an `AnimatorOverrideController` adapter that maps the player
  controller's states to the enemy's own idle/walk/death clips.
- Or use a small procedural "bob/roll" driver and accept that the enemy does
  not play the full player animation set.

A generic adapter would cover Balloom and its many recolours first, then Jump,
Bomb, Armor, etc. Vehicles/objects such as Drill, Eater, Magnet and Shot are
poor fits and should be lower priority.

### Tier D — Boss robots and giant bosses

**Out of scope.** `boss_robot_pack_*` contains large, separately controlled
robots with `BossRobotController`, custom part/animation data and no player
skeleton. Making one playable is a custom character controller project, not a
character-skin port.

## Implementation design

### 1. Runtime character rows

Append proxy rows to the built-in table at `CharacterDataManager.init` time:

- New `CharacterInfoData` with `charKey` like `npc_boy` / `enemy_hige`,
  `contentType = BUILTIN`, next free `charId` (51+), sort order after 50.
- Reuse the host character's status ID, voice, lines and save reference until
  dedicated data exists.
- Add the row to:
  - `_sortedCharacterInfos` (what character select iterates),
  - `CharacterInfoDataTable_builtin._characterInfoDatas`,
  - `_charKeyToDataTbl` and `_charIdToDataTbl`,
  - the category dictionary used by `getCharacterIdsByCharacterCategory`.
- `getTotalCharacterCount()` and `CharacterSelectDialog.getCharacterDataLength()`
  already read `_sortedCharacterInfos.Length`, so an appended array is visible
  to the dialog.
- If category tabs ignore appended rows, patch
  `getCharacterCountByCharacterCategory` / `getCharacterIdsByCharacterCategory`
  to include proxies.

This is the main technical unknown. Validate it with a single Boy row before
building the visual swap.

### 2. Usability and names

- Patch `isUsableCharacterInternal` and `isUsableBuiltinCharacter` for proxy
  keys, the same way `PlatformCharacters` exposes M001/S001/N001.
- Inject names into `LocaleDataTable` (`PLAYABLE_NPC_BOY`,
  `PLAYABLE_ENEMY_HIGE`). The current platform-name code only edits existing
  indexes; proxy rows need either appended locale entries or a reused name key.
- Keep proxy characters **local/offline only**. Do not allow them in online or
  Grand Prix rooms, where other clients do not have the same table.

### 3. Model attachment

There are two practical routes.

**Route 1 — attach the target model to a player prefab (fast prototype).**

- Host prefab: an existing player, e.g. `model_pc001_white`.
- Keep all `PlayerComponent`, `MeshCustomizer`, Animator and effects.
- Disable the host body renderers and add the NPC/enemy prefab as a child.
- Bind the target's Animator to the host controller, or attach `PlayerComponent`
  to the target root and make it the driven model.
- For Boy, the identical rest pose means the target's own skeleton can be used
  and animations will look correct.
- For Hige, use the target's own skeleton and a scale/offset correction; do not
  rebind the Hige mesh onto player bones unless the result is tested.

**Route 2 — offline hybrid prefabs (cleaner ship).**

- Build Unity 5.6 prefabs with the player component set plus the target
  mesh/animator.
- Put them in a patch asset bundle.
- Point the proxy rows at that bundle and prefab.
- No runtime component surgery; easier to validate in the Unity editor, but
  requires a 5.6 bundle-building workflow and distribution of modified assets.

Recommendation: prove Route 1 first, then decide whether Route 2 is worth the
tooling.

### 4. Animation

- Tier A/B: copy a host player's `AnimatorOverrideController` to the target
  Animator.
- Tier C: create an adapter controller per enemy, or use the enemy's own
  controller for idle/walk and skip player-specific states.
- Player logic sets parameters/triggers on `CharacterModel._anim`; the target
  must be that animator, not a hidden host animator.
- Boy's `boy_help` clip is not a player state and can be ignored for the
  playable version.

### 5. Icons, preview and results

- **Icon:** reuse the host player icon for the prototype. For polish, render a
  thumbnail or extract an enemy portrait from the UI pack.
- **Preview:** the character-select preview loads `charPrefab` directly, not
  through `CharacterModel`. A proxy row that points at a host prefab will show
  the host. Either point the row at the target prefab (if it can render
  statically) or hook the preview creation separately.
- **Results:** reuse the host result icons until custom ones exist.
- **Topknots:** Boy and Hige have no `knot_root`. Disable accessory selection
  for proxies, or create an empty `knot_root` and accept that hats float in the
  wrong place.

### 6. Save data

`CharacterID` is `contentType + byte`, so IDs 51+ work and save/load naturally.
Old saves do not contain proxy IDs and are unaffected. A save created with a
proxy character will show that character as long as the plugin is installed;
without the plugin it should fall back to a valid host row.

## Validation matrix

- Character select: row appears, name, icon, preview, category tab.
- Save/reload and story character selection.
- Local battle P1/P2 and CPU selection.
- Story stage, practice, randomizer interaction.
- Bomb spawn/throw position (`bom_pos`).
- Idle/run/bomb/special/damage/death animations.
- Item pickup, invincibility, revenge cart.
- Topknot/accessory UI disabled or safe.
- Low-end/ultra-low texture modes.
- Online/GP blocked.
- Performance and memory with each extra model.

## Effort summary

| Scope | Effort |
|---|---|
| Boy prototype (row + swap + host icon) | 2–4 days |
| Hige prototype | 3–6 days |
| Polish both (icons, preview, names, save, P2/CPU, topknot gating) | +1–2 weeks |
| Balloom-family animation adapter | +1–2 weeks |
| Full partial-rig enemy roster | 3–5 weeks |
| Boss robots | Not recommended |

## Recommended order

1. **Boy** — prove row injection, selection, save and model attachment with a
   rig that is identical to the player.
2. **Hige** — prove the enemy path with the only full enemy rig.
3. **Balloom + recolours** — build the animation adapter once, reuse it.
4. Re-evaluate the remaining enemies after the adapter exists.
5. Keep boss robots and giant bosses out of the playable roster.

## Open questions

1. Do appended `_sortedCharacterInfos` rows survive the category tabs, or does
   the category dictionary need a patch?
2. Is a runtime-added `PlayerComponent` enough, or should the target Animator be
   driven through the host component?
3. Do player animations look acceptable on the Hige rest pose without per-bone
   offsets?
4. Should proxy characters use their own save-reference key, or reuse the host
   character's flags?
5. Do we want custom icons/previews in the first release, or ship with host
   icons as a prototype?
6. Should proxies be allowed in local battle only, or also in story saves?
