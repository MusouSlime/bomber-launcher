# Offline backlog

Status: current offline feature audit. The core offline QoL suite is already
usable; what remains is polish, existing-content coverage, and optional new
content.

Networking is deliberately shelved until there are enough players for a
two-client test.

## Already complete

- Launcher: game-folder detection/override, BepInEx install, launch options,
  aspect ratio, language byte, Japanese voices, intro skip, save backup,
  restore originals.
- Plugin: F9/main-menu overlay, practice stage/difficulty/stats/repeat,
  Random Run, random characters/enemies/pickups, pickup toasts/blank icons,
  move-set randomizer hooks, character-select skip, gem-continue skip.
- Runtime QoL: post-FX toggles, fast results, Master difficulty, movie skip,
  auto-skip, soft reset, custom BGM/SFX, platform-exclusive characters.
- Multiplayer local: smarter bots, random CPU characters, CPU specials.
- Performance: Low-End (60 FPS default, 30 optional, half textures, shadows
  off) and Ultra-Low (960×540, quarter textures, no pixel lights, LOD 2).
- In-game options sync for the performance tiers.
- Character skins/accessories were deliberately removed from `master`; they
  are preserved on local branches, not part of the offline backlog.

## P0 — finish the offline build

These are the items needed before calling the current offline feature set
“complete.”

1. **Low-End / Ultra-Low verification pass**
   - 960×540, 1280×720, 1920×1080 and 4K.
   - All five F9 tabs, controller and keyboard navigation.
   - In-game Options reflect and restore the texture/AA/shadow/light values.
   - Resolution restore on disable/exit; confirm 60 FPS default and optional
     30 FPS cap.
   - Windows parity for the same toggles.

2. **Save/config safety pass**
   - Random Run suppresses story saves; verify after crashes, alt-tabs and
     mid-battle exits.
   - Verify config recovery when the plugin is removed or the game updates.
   - Confirm no stale session flags carry into normal story/practice.

3. **F9 / UI polish**
   - 4K and ultrawide font/row scaling; the overlay is full-screen but fonts
     are still IMGUI defaults.
   - Per-tab focus memory.
   - Verify the three-column Settings tab on 1280×720 and 960×540.

4. **Windows launcher parity**
   - Windows artwork extraction (Linux has it; Windows expects a manually
     copied `assets/` folder).
   - Confirm Low-End/Ultra-Low switches and CLI presets match Linux.

5. **Release hygiene**
   - Offline feature docs and screenshots refresh.
   - Checksums/release notes for the next version.
   - Decide whether the untracked SBR2 tools/docs belong in this repo or a
     separate track.

## P1 — existing game content (highest value)

No new assets; use what ships with the PC build.

1. **Arena intermission for Random Run**
   - Insert one of the 60 multiplayer maps every N stages.
   - Synthetic story map row, annihilate goal, enemy budget, no boss.
   - Prototype: 3–5 days. Robust version with rewards/camera/balance: 1–2
     weeks.

2. **Enemy randomizer v2**
   - Global/cross-map enemy pools, strong/weak variants, per-stage budgets,
     no repeats, boss minions.
   - Uses existing enemy packs; 2–4 days.

3. **Random Run 2.0**
   - Seed selection/display, stage filters (biome/layout), modifiers
     (no pickups, one life, double enemies, mirror controls), run summary and
     history.
   - 1–2 weeks for the first useful set.

4. **Local battle coverage**
   - Expose all existing rules/maps in local battle, including Grand Prix
     rules (crystal, checkpoint, escort, classic).
   - Team selection and CPU teammates.
   - CPU AI fallback for platform/DLC characters (experimental).
   - 1–2 weeks.

5. **Practice quality**
   - Named loadout presets.
   - Quick restart current battle, skip to boss.
   - 2–4 days.

6. **Existing unlock content decision**
   - Cassettes (32) and topknots (305) are present but gated.
   - The old “unlock all characters” plan was rejected as a cheat. If this is
     still the policy, leave them locked. If not, make it an explicit opt-in
     gallery, not a default toggle.
   - 2–3 days if approved.

## P2 — new content

These are new playable content projects, not polish.

1. **Playable Boy NPC**
   - Add a proxy character row, attach `model_np001_boy`, add `bom_pos`/
     `knot_root`, use a player Animator, host icon fallback.
   - 2–4 days for a prototype; +1–2 weeks polish.

2. **Playable Hige enemy**
   - Only enemy with the full player skeleton.
   - Add player components, disable `EnemyComponent`, tune scale/offset,
     test bomb/death/special animations.
   - 3–6 days after the Boy pipeline.

3. **Balloom-family animator adapter**
   - Partial rigs; needs a controller mapping player states to enemy clips.
   - 1–2 weeks.

4. **Custom stage generator**
   - Clone/author `MapData`, generate blocks/spawns/items, new table rows and
     Random Run pool.
   - 2–4 weeks for a flat-arena generator; themed/gimmick maps longer.

5. **Out of scope**
   - Boss robots/giant bosses as playable.
   - Full new story chapters/dialogue.
   - Network transport replacement (shelved with networking).

## P3 — launcher extras

- Save profile manager: named backups, import/export, restore.
- GitHub auto-update check.
- Flatpak/Steam Deck packaging.
- Localization of injected names (currently English only).
- Crash/log viewer and config validation.
- Apply config changes without a full game restart (hard; likely skip).

## Suggested finish line

**Offline 1.0:** current suite + P0 + arena intermission + enemy randomizer v2
+ Random Run 2.0 + all local battle maps/rules.

**Offline 1.1:** playable Boy/Hige and the Balloom adapter.

**Later:** custom stage generator, launcher extras, optional unlock gallery.

## Decisions needed

1. Is Offline 1.0 the finish line above, or do playable NPCs/enemies belong in
   the first offline “complete” release?
2. Arena intermission vs a separate Arena Run.
3. Keep cassettes/topknots locked, or add an explicit opt-in gallery?
4. Keep the SBR2 track separate from the SBR1 launcher repo?
5. How important is 4K/ultrawide F9 scaling versus new content?
