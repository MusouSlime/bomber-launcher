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

## Deep dive: player/bomb pass-through (lead #1)

### What the code exposes

**Timed pass-through states** (`AbstractPlayer`):

```text
SlidingThroughBombParam:
  float _timer
  startSlidingThroughBomb(float time)
  endSlidingThroughBomb()
  isSlidingThroughBomb()
  update(float deltaTime)

BombStunDisabledParam:
  float _timer
  startBombStunDisabled(float time)
  endBombStunDisabled()
  isBombStunDisabled()
  update(float deltaTime)

InvincibleParam:
  bool isNeedEffect, isNeedEndEffect
  float _timer
  bool _isValidPermanently
  startInvincible(float time, ...)
  startStunInvincible(float time)
  setPostFallInvincible() / resetPostFallInvincible()
```

These are time-window permissions, not permanent flags. A speedrun-relevant
bug is likely a way to **refresh, stack, pause, or re-enter** one of these
windows so the player can cross bombs/fire without the intended timing.

**Collision shape** (`CollisionUtils.FanShape`):

```text
Vector3 p          // origin
float radius
Vector2 normalizedDir
float halfAngle
float halfHeight
bool isHitPoint(Vector3 point)
AABB getAABB()
```

`PlayerMaster.checkHit(FanShape)` is a point-in-fan style test. It is not a
swept capsule. Fast movement between frames can put the player on the other
side of a bomb/fire/wall corner without a sample inside the shape.

**Movement** (`CharacterMove` / player move states):

```text
_speed, _dashDirection, _dashStartPosition, _dashEndPosition
_dashDistance, _dashTimer, dashTimer, speed
moveDiff(ref Vector3 diff, out bool moveDone, Vector3Int direction)
```

Dashes are timed with start/end points, which makes them the best candidate
for tunneling: the dash can cover more distance per collision sample than the
fan shape.

### Candidate glitches

A. **Dash tunneling through a bomb/fire**
   - Dash through a bomb or flame at the last moment.
   - The player's sampled position can be outside the fan on both sides.
   - Test all dash characters and speed levels.

B. **Sliding-through-bomb window refresh**
   - Find the skill/effect/item that calls `startSlidingThroughBomb`.
   - Re-trigger or extend it before it expires.
   - Pausing may freeze the timer while the effect remains active.
   - Test each character's special and rule effects.

C. **Bomb-stun immunity refresh**
   - `startBombStunDisabled` blocks bomb stun for a window.
   - If a skill can refresh it, walk through explosions.
   - Test with characters whose skill grants bomb immunity.

D. **Post-fall invincibility**
   - `setPostFallInvincible` / `resetPostFallInvincible` grant a window after
     falling into a pit.
   - Test: fall, respawn, immediately cross fire or take a hit.
   - May allow a damage boost or a safe path through a hazard.

E. **Collision cooldown / state gate**
   - Collision is handled in `entryCollision`/`execCollision` states.
   - Some states have `_collisionTime` timers (`CastleArcState`).
   - Test whether entering/leaving a state during a hit skips the hit.

F. **Fan-shape corner clip**
   - Move diagonally past a corner at the fan's edge (`halfAngle`/
     `halfHeight`).
   - Test all four corners at normal speed and dash speed.

### Test matrix

| Test | Setup | Measure |
|---|---|---|
| Dash vs bomb | Bomb at a known tile, dash across at 1/2/3 tiles | damage, position |
| Dash vs fire | Flame at max firepower, dash through | damage, position |
| Slide window | Character skill that grants slide | timer, damage |
| Stun immunity | Bomb-stun-disabled skill/effect | timer, damage |
| Post-fall | Fall into a pit near fire | invincible timer, damage |
| FPS | 30 / 60 / 144 / uncapped | same inputs, position/damage |
| Pause | Pause during slide/stun/invincible | timer remaining |

### Instrumentation

Log every frame while testing:

```text
time, frameCount, Time.deltaTime, Time.timeScale
player position, map index, speed level, dash state/timer
isSlidingThroughBomb, isBombStunDisabled, isInvincible,
  isPostFallInvincible
nearby bomb positions, fuse timers, fire tiles
```

Do not modify values in the audit build; only read them. The audit plugin
should run offline/private.
