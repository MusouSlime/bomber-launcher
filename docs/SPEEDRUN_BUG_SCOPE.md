# SBR2 speedrun bug scope

Read-only code scoping for offline/private speedruns. No online or ranked use.

## Method and limits

The plugin builds against BepInEx 6 interop assemblies. Those are **managed
wrappers** around native IL2CPP code: class layouts, fields, and method
signatures are visible, but method bodies are `il2cpp_runtime_invoke` calls.
So the items below are **candidate bugs/leads**, not confirmed glitches.

Confirmation requires runtime instrumentation or native decompilation
(Ghidra/IDA on `GameAssembly.dll`).

## High-priority leads

### 1. Player/bomb pass-through and collision

Evidence:

- `AbstractPlayer.isSlidingThroughBomb()`
- `AbstractPlayer.isBombStunDisabled()`
- `PlayerMaster._collisionTime`
- `PlayerMaster.checkHit(CollisionUtils.FanShape)`
- `AbstractPlayer.update(float deltaTime)` / `PlayerController.update(areas, deltaTime)`

Hypothesis: sliding/pass-through states, the fan-shaped collision sweep, or
the collision timer can let the player cross a bomb, fire, or wall corner.
Test at 30/60/144/uncapped FPS and with speed power-ups.

### 2. Bomb slide, landing, and warp

Evidence:

- `AbstractBombController.startEarlySlide(mapIdx)`
- `AbstractBombController.syncSlide(mapIdx, resultSlideBounceParam)`
- `AbstractBombController.update(ref deltaTime, dir, speed, ref position)`
- `AbstractBomb.enableWarp()`
- `AbstractBomb.calcFlightDst`, `calcBounceDst`,
  `calcBounceDstMapIdx`, `calcWarpDstMapIdx`
- `BombMaster.landingFromSlide`, `punchBomb`

Hypotheses:

- Early slide/landing can put a bomb through a wall or area boundary.
- `enableWarp` plus a thrown/punched bomb can move the bomb (and its blast)
  into an adjacent area without the normal transition.
- Bounce/warp destination calculation can be tricked at map edges.

### 3. Fuse, chain reactions, and remote bombs

Evidence:

- `BombMaster.begin(..., time, firepower, ..., randomSeed, preAppearBombId)`
- `AbstractBomb.setTime(time, isUndoExploded)`
- `AbstractBomb.deactivateTimer()`
- `BombMaster.onExplosionBomb`, `FireControllerHost.onExplosion`
- `AbstractBomb.update(deltaTime)`

Hypotheses:

- Pause/slowdown or frame-rate changes can alter fuse timing.
- Chain-order or `preAppearBombId` can skip a fuse or duplicate an explosion.
- `deactivateTimer`/remote states may leave a live bomb without a timer.

### 4. Goal, warp terminal, and stage clear

Evidence:

- `GoalComponent`
- `MapController.findCollisionFloor`
- `WarpTerminalChargeEffect` (`STATE`, `isReady`, `_chargeIndex`,
  `entryCharging`/`execCharging`)
- `WarpTerminalModelComponent.activate/pause/resume`

Hypotheses:

- Goal overlap can be reached through a wall/corner with the collision sweep.
- Warp charge can be cancelled/restarted or skipped by state changes.
- Pausing during the charge may preserve charge progress.

### 5. Boss damage and phase skips

Evidence:

- `BossStatus.addBossDamage(int)`, `isDefeatDamage(int)`
- `BossController.addDamage(int, float)`, `isDefeatDamage`
- `BossInvincibleBehaviour`
- `BossStunnedAction`, `BossStunUpdate`
- `LastBossStageController`

Hypotheses:

- Burst damage can overflow/ignore a phase gate.
- Stun-lock or invincibility-state timing can skip an attack phase.
- `isWeakened` or shield states can be bypassed by damage order.

### 6. Save/quit and story checkpoints

Evidence:

- `StoryManager.saveData(SAVE_POINT)`
- `StoryManager.saveDataWithInterruptedExplore(...)`
- `StoryManager.saveDataWithInterruptedCamp(...)`
- `StoryManager.setFromSaveData(SaveStoryData)`
- `SaveStoryData.SAVE_POINT`

Hypotheses:

- Quitting at a save point or after a cutscene can place the checkpoint
  later than intended.
- Interrupted explore/camp saves may preserve items, EXP, or flags across a
  reset.
- Story flags may update before the associated cutscene finishes.

### 7. Cutscene, pause, and scene transition

Evidence:

- `MoviePlayUI.onSubmitSkipButton`, `isPlaying`, `isPausable`
- `CutsceneManager`
- `SceneTransition.begin/overwriteState`
- `PlayerController.requestRestartByPause`
- `Pause`/`Result` menu classes

Hypotheses:

- Pause/restart during a transition can skip a fade or scene load.
- Movie skip can fire before a flag is committed.
- Fast results/rematch can skip end-of-battle bookkeeping.

### 8. RNG and spawn order

Evidence:

- `BombMaster.begin(..., randomSeed, ...)`
- `MapRandomItemDeployProcess`
- `BossScheduler`, `BossActionRandomPositionGetter`
- `StoryRewardManager`

Hypotheses:

- The per-bomb `randomSeed` or item deploy RNG can be influenced by frame
  timing, pause, or actor order.
- Boss position/attack RNG can be manipulated by entry timing.

## Medium-priority leads

- `AbstractPlayer.calcSpeed` and `onChangePlayerSpeedLv` — speed-level rounding.
- `PlayerItem`, `ItemController`, `PlayerItemDropController` — duplicate or
  lost item drops on area change.
- `MapLoader`, `MapData`, `requestAreaChange` — area-boundary transitions.
- `PlayerController.requestDie`, respawn timers — death warp/respawn order.
- `AbstractBomb.enableWarp` + `MapController.findCollisionFloor` — bomb
  collision at area seams.
- `SceneTransition.overwriteState` — forcing transition state.

## Test plan

1. **Backup saves** before any story test.
2. Add a read-only audit plugin that logs:
   - player position/state, `isSlidingThroughBomb`, speed level
   - bomb positions, fuse timers, `enableWarp`, slide state
   - goal/warp-terminal state, area index, scene name
   - boss HP/phase/invincibility
3. Run the same input macro at 30/60/144/uncapped FPS.
4. Test the first story stage for:
   - corner/bomb pass-through
   - bomb slide at walls/area seams
   - goal overlap
   - warp charge cancel
5. Test save/quit at each `SAVE_POINT` and after each cutscene.
6. Record video with frame stepping; compare inputs/positions.

## Confirmation levels

- **Lead** — visible in the API/classes only.
- **Reproducible** — same inputs produce the same result.
- **Useful** — saves meaningful time in a real run.
- **Allowed** — offline/private only.

## Recommended first target

Bomb sliding + player collision at area seams. It has the clearest code
surface (`startEarlySlide`, `syncSlide`, `enableWarp`,
`isSlidingThroughBomb`) and the highest chance of a repeatable pass-through.
