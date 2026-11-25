# SBR 2: custom music, SBR1 character ports, story character select

Three separate tracks, in increasing order of effort:

1. **Custom BGM** — easy/medium, port the existing SBR 1 BomberQoL approach.
2. **Story character select** — easy/medium, one central hook.
3. **Porting SBR 1 characters** — easy as a skin swap, medium-hard as a real new
   roster slot, hard as a full moveset/animation port.

---

## 1. Custom music

### What the game uses

BGM lives in `StreamingAssets/InAppAssets/Win64/sounds/bgm/*`. Each bundle
contains several `AudioClip`s, e.g. `snd_bgm_str_explore_01` contains:

- `BGM_sty_stg_01_a`
- `BGM_sty_stg_01_b`
- `BGM_sty_stg_01_c`

They are 48 kHz stereo, Vorbis, streaming (`m_LoadType = 2`), roughly 80–100
seconds each. The game also has `sounds/movie/*` for movie audio and
`sounds/voice/character_e|j`, `story_e|j`.

`SoundManager` (IL2CPP) exposes almost exactly the same BGM API as SBR 1:

- `playAsBGM(string bgmLabel, float fadeTime)`
- `playAsBGM(string bgmLabel, bool forcePush, float fadeTime)`
- `playCrossFadeAsBGM(...)`
- `changeBGM(string bgmLabel, float fadeTime)`
- `changeCrossFadeBGM(...)`
- `restartBGM`, `stopBGM`, `onPauseBGM`, `offPauseBGM`
- `popBGM`, `popCrossFadeBGM`
- `tryGetCurBGMInfo`, `tryGetCurBGMSpectrum`, `isPlayingBGM`, `isStoppedBGM`

This is the same surface the SBR 1 `CustomBgmPlayer` in
`plugin/BomberQoL.cs` already patches. Porting it is mostly a BepInEx 6 IL2CPP
rewrite, not a new design.

### Two ways to replace music

| Approach | Effort | Notes |
| --- | --- | --- |
| **Runtime plugin** (recommended) | Easy–Medium | Hook the `SoundManager` BGM methods, load external `.ogg`/`.wav` from a folder, play through your own `AudioSource`, fall back to the original track. No bundle edits. |
| **Bundle replacement** | Medium | Import the new audio into the `AudioClip` in `sounds/bgm/...` with UABEA, repack, re-encrypt. Permanent; makes 100 MB+ redistributable bundles; loop points/format must match. |
| **Both** | — | Plugin for user playlists; bundle replacement for a fixed soundtrack mod. |

### Runtime plugin behavior

- Folder of `*.ogg` / `*.wav` files.
- Match by label:
  - exact: `BGM_sty_stg_01_c.ogg`
  - base label fallback: `BGM_sty_stg_01.ogg` matches `_a/_b/_c`
  - optional playlist/shuffle when no exact match
- Load with `UnityWebRequestMultimedia.GetAudioClip("file://...", AudioType.OGGVORBIS)`.
  (`WWW.GetAudioClip` also works but is older; Unity 2020 has the newer API.)
- Own `AudioSource` with `loop = true`, configurable volume.
- Keep a label stack so `popBGM` restores the previous custom track.
- On a missing file or decode failure, `return true` from the Harmony prefix so
  the original `SoundManager` call runs.
- Hook `playAsBGM`, `playCrossFadeAsBGM`, `changeBGM`, `changeCrossFadeBGM`;
  forward `stopBGM`, `onPauseBGM`, `offPauseBGM`, `restartBGM`, `popBGM`.

Pseudo-prefix:

```csharp
[HarmonyPatch(typeof(SoundManager), "playAsBGM", new[] { typeof(string), typeof(float) })]
static class BgmPatch
{
    static bool Prefix(SoundManager __instance, string bgmLabel, float fadeTime)
    {
        return !CustomBgm.TryPlay(__instance, bgmLabel, fadeTime, change: false);
    }
}
```

### Caveats

- BGM visualizers using `tryGetCurBGMSpectrum` will still see the original clip
  unless you also copy samples into the original source.
- Movie audio and voice are separate; the plugin only replaces BGM labels.
- `changeBGM` vs `playAsBGM` semantics differ (replace current vs push); keep the
  same distinction in the custom player to avoid stack drift.
- Custom tracks are user-supplied; do not redistribute copyrighted music.

---

## 2. Selecting different characters in story mode

### The hook

`StoryManager.getStoryCharacterId(int joinPlayerIndex)` returns the
`CharacterDataManager.CharacterID` used to build the story player. It is called
by:

- `StoryManager.getCastleAttackParam`
- `StoryManager.getBaseDefenceParam`
- `StoryManager.createExploreParamInternal`
- `StoryManager.createBossParamInternal`
- `StoryManager.createLastBossParamInternal`
- `StageEditLogic.createTestPlayParam`

So changing its result changes the player character in **every** story battle
mode: explore, puzzles, bosses, Castle, base defence, last battle.

`CharacterDataManager` has a `getCharacterId(string key)` method and the
`CharacterInfoDataTable` contains 38 keys:

```
white black pink blue red yellow green aqua
simon pyramid richter lance bill mimi nyami alucard
vicviper nurse lobby tomato nakedSnake raiden oldsnake
vicviperzero somaCruz fujisaki genso2p goemon
magnet tinyGolem phantom sexy plasma
fiter Massive wing bean lucaBlight
```

Pseudo-postfix:

```csharp
[HarmonyPatch(typeof(StoryManager), nameof(StoryManager.getStoryCharacterId))]
static class StoryCharacterPatch
{
    static void Postfix(int joinPlayerIndex,
                        ref CharacterDataManager.CharacterID __result)
    {
        if (joinPlayerIndex != 0) return;
        var key = Plugin.StoryCharacterKey.Value;
        if (!string.IsNullOrEmpty(key))
            __result = CharacterDataManager.instance.getCharacterId(key);
    }
}
```

### UI options

1. **Launcher/config selection** (easy): a dropdown of the 38 keys; the plugin
   reads the key and applies it on story load. No new UI inside the game.
2. **In-game story character select** (medium-hard): reuse the battle-mode
   character select UI before starting/continuing a story, or add a camp menu
   entry. Needs UI hooks and story state handling.
3. **Per-save character** (medium): store the chosen key in the launcher’s
   settings or a sidecar file, not in `StoryData`; the plugin applies it on load.

### Caveats

- **Cutscenes still show White.** Pre-rendered movies and likely the story
  cutscene actors are White-specific. Gameplay will use the chosen character;
  story dialogue/portraits may not.
- Story stats/leveling use `StoryCharacterStatusDataTable`; the chosen
  character may get its own story stats or White’s depending on the param path.
  Test `white` → `simon`/`pink` first.
- Size/collision: guest characters with unusual proportions may clip in tight
  story maps. The shared Bomberman rig means most Bomber Bros are safe.
- Voice: gameplay voice comes from `sounds/voice/character_e|j`; cutscenes may
  still use White’s voice.
- Online/story sharing: story is single-player/offline; low risk.

This is the **first thing worth shipping**: it is one hook plus a config entry,
and it immediately fixes “story mode only lets you play White.”

---

## 3. Porting characters from SBR 1

### Good news: the rigs match

SBR 1 and SBR 2 use the same naming and skeleton:

- SBR 1: `model_pc001_whiteAvatar`, `model_pc009_prettyAvatar`, ...
- SBR 2: `model_pc001_white_bodyAvatar`, `model_pc001_white_headAvatar`, ...
- Shared bones: `C_waist`, `C_spine`, `C_head`, `L_hand`, `R_hand`,
  `L_shoulder`, `R_shoulder`, `L_foot`, `R_foot`, `bom_pos`, `face1Pos`,
  `face2Pos`.

SBR 2 splits characters into head/body/knot/left-hand/right-hand prefabs, and
has `_G4`/`_G5` material variants. SBR 1 models are body/head/hand assets with
legacy materials, but the bone names and `Avatar` naming line up.

This means the practical path is:

- reuse SBR 2’s **Animator/Avatar/animations** on the ported mesh;
- convert SBR 1 materials to SBR 2 `_G4`/`_G5` shaders;
- rebuild the SBR 2 prefab structure (`*_head_root`, `*_body_root`,
  `*_knot_root`, `*_Lhand_root`, `*_Rhand_root`).

### What is already in SBR 2

SBR 2’s `CharacterInfoDataTable` already contains many SBR 1 characters:

White, Black, Pink, Blue, Red, Yellow, Green, Aqua, Simon, Pyramid, Alucard,
Vic Viper, Nurse, Tomato, Naked Snake, Old Snake (Solid Snake), Raiden,
Vic Viper Zero, Fujisaki, Goemon, Bill, Lance, Mimi, Nyami, Magnet,
Tiny Golem, Phantom, Sexy, Plasma.

SBR 1 avatars **not** matched by an SBR 2 key (some are NPCs/assists, not
necessarily playable):

```
Pretty (pc009_pretty)
Dracula, Ebisu, Katagiri, Option, Tako, Gold Viper,
Hinomoto, Jehuty, Anubis, Master Chief, Arbiter, Grunt,
pBody, ATLAS, Ratchet, Xaviar Woods, N001, Upa, Toro,
Piposaru, Ceaser, and the Kirakira White/Black/Blue/Red/
Yellow/Green/Pink/Lightblue variants.
```

`Golem` is ambiguous: SBR 2 has `phadom`; verify whether that is the intended
Golem replacement before porting `bo002_golem`.

The standout missing Bomber is **Pretty Bomber**. She is the best first
porting target: recognizable, same rig, and easy to test as a skin swap.

### Three porting levels

| Level | What you get | Effort | Approach |
| --- | --- | --- | --- |
| **Skin swap** | A ported model replaces an existing SBR 2 character’s mesh/material. UI/name/stats stay the slot’s. | Easy–Medium | Replace `pink` with `pc009_pretty` meshes/materials; keep SBR 2 avatar/animations. |
| **New roster slot** | A real selectable character with its own name/icon/stats/skill. | Medium–Hard | Add/duplicate `CharacterInfoDataTable`, `CharacterStatusDataTable`, `CharacterUIDataTable`, `CharacterReleaseConditionDataTable`, `ShopProductTable`; import prefabs; reuse SBR 2 animations. |
| **Full character port** | Unique moveset/animations/voice/skill. | Hard | Import SBR 1 AnimationClips/AnimatorController, retarget, import skill assets/voice, test every story/battle state. |

**Important:** `CharacterID` is a compiled enum. Adding a brand-new ID is not
free; the safest “new character” is to reuse an existing/unused enum slot or
replace an existing character’s assets and rename it through localization. A
skin swap avoids enum/UI risk entirely.

### Asset pipeline for a Pretty Bomber swap

1. Decrypt SBR 1 `chara_pack_01`:
   ```sh
   python3 tools/sbr_pack.py dec -i <chara_pack_01> -o /tmp/sbr1_chara01.unity3d
   ```
2. Export Pretty’s assets with AssetStudio/AssetRipper:
   - `model_pc009_pretty` prefab / meshes
   - `mat_pc009_pretty_*` materials
   - textures
   - `model_pc009_prettyAvatar`
3. Decrypt SBR 2 `chara/chara_001`:
   ```sh
   python3 tools/sbr2_pack.py decrypt \
     "$SBR2/.../Win64/chara/chara_001" /tmp/sbr2_chara001.unity3d
   ```
4. In UABEA/UnityPy, replace the `pink` body/head/knot/hand meshes and
   materials with Pretty’s, keeping the SBR 2 prefab hierarchy and renderers.
   **Caveat:** SBR 1 Pretty is a single `mesh_pc009_pretty_body` plus
   `ac009_pretty_mic`; SBR 2 expects separate head/body/knot/hand prefabs. You
   will need to split the head from the body mesh or remap the head/body
   renderers. This is the main asset work, not the material conversion.
5. Convert materials to SBR 2 `_G4`/`_G5`; copy textures and set the shader
   properties. If the SBR 1 material uses a legacy shader, create a new SBR 2
   material and assign the same albedo/normal/mask textures. SBR 2 `pink` has
   `_H`/`_G4`/`_G5` variants; pick the quality tier your test map uses.
6. Save, re-encrypt the `chara_001` header, replace the game file, and test:
   - offline battle first;
   - then story select `pink` and verify animations/collision.
7. Optional: edit `CharacterUIDataTable`/localization to call the slot
   “Pretty Bomber” instead of “Pink Bomber.”

For a **new slot**, duplicate the `pink` row in `CharacterInfoDataTable`, change
the key/prefab paths, duplicate `CharacterUIDataTable`/status/release rows, and
add a shop product. Keep the same `CharacterID` enum value as a safe base until
you know how the UI/state machines handle a new enum value.

### Testing checklist

- Offline Standard battle: model, animations, materials, hands, bomb position.
- Story select hook: White → Pretty/Pink/Simon.
- Story cutscenes: still White? Accept or patch separately.
- Boss/Castle/base-defence story scenes: model size/collision.
- Save/reload and character select persistence.
- Online: do not use gameplay/character mods in crossplay/ranked.

---

## 4. Recommended order

1. **Story character select hook** — one `getStoryCharacterId` hook + config/UI;
   immediate win, no assets.
2. **Custom BGM plugin** — port the SBR 1 `CustomBgmPlayer` to BepInEx 6 IL2CPP;
   the `SoundManager` API is the same.
3. **Pretty Bomber skin swap** — proof of the SBR 1 → SBR 2 asset pipeline.
4. **Pretty Bomber as a real slot** — data/UI/asset work after the swap works.
5. **Other missing characters** — prioritize by rig similarity and licensing;
   guest characters may not be distributable.

These three features are independent: custom music, story character choice, and
character assets can be developed and tested separately.
