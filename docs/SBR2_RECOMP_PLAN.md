# Super Bomberman R 1/2 — recomp / port feasibility plan

Status: planning only. No recomp work started; the current focus is the PC
BepInEx 6 QoL/custom-content plugin.

---

## 1. First, define “recomp”

Three different things get called “recomp,” and they have very different costs:

| Approach | What it means | Closest example |
| --- | --- | --- |
| **Static recompilation** | Translate the original machine code (e.g. Switch ARM64) into C, link it to an official Unity player of the same version, and reimplement the platform APIs/shader path. | **Marble Knights Recompiled** (iOS ARM64 Unity IL2CPP → Windows) |
| **Decompilation / source reconstruction** | Recover C# from Mono/IL2CPP, rebuild a Unity project with extracted assets and reimplemented platform services. | Typical Unity fan ports / decomp projects |
| **Asset port / remake** | Extract models, textures, audio and data, then rebuild the game in a new engine or Unity project. | Fan remakes, not recomp |

The user-facing question “would that let us port mods to other platforms?”
depends on which of these we mean.

---

## 2. The Marble Knights precedent

`YuutaTsubasa/MarbleKnightsRecompiled` is the important proof of concept:

- Apple Arcade **iOS, Unity IL2CPP, ARM64**.
- Translates the iOS app’s `UnityFramework` — the IL2CPP runtime plus the
  game’s compiled C#, **over 100,000 functions** — ahead-of-time into C.
- Builds a `GameAssembly.dll` that the **official Unity 2019.4.17f1 Windows
  player** loads.
- Uses the game’s `global-metadata.dat` as-is and retargets serialized files to
  `StandaloneWindows64`.
- Reimplements iOS system calls, Game Center, CloudKit saves, controllers,
  haptics, audio output and the Metal→GLSL shader path.
- “Bring your own IPA”; no game data in the repo.
- Windows and Android only so far; Linux/macOS not started.

That is a real static recomp of a Unity IL2CPP game. It is also a
game-specific, specialist project, not a general Unity recompiler.

---

## 3. SBR 1 vs SBR 2 technical shape

### Super Bomberman R (2018)

| | |
| --- | --- |
| PC build | **Unity 5.6.5, Mono, x86** |
| Managed code | `Assembly-CSharp.dll` — decompilable to close-to-original C# |
| Assets | encrypted UnityFS bundles under `InAppAssets/steam` (already reverse-engineered) |
| Renderer | legacy/older Unity pipeline |
| Console build | likely Unity 5.6.5 IL2CPP on Switch (verify) |

**Implication:** SBR 1 is the easier “source reconstruction” target. Mono +
decompilable `Assembly-CSharp.dll` means a fan source port is plausible.
Static recomp from the Switch ARM64 build is possible in principle but awkward:
you would need a matching Unity 5.6 player/runtime and a large platform layer.

### Super Bomberman R 2 (2023)

| | |
| --- | --- |
| PC build | **Unity 2020.3.36f1, IL2CPP x64** |
| Binary | `GameAssembly.dll` + `global-metadata.dat` |
| Assets | header-encrypted UnityFS bundles under `InAppAssets/Win64` (cracked) |
| Renderer | **HDRP + HexaDrive `Fuse` HDRP extension** |
| Online | Unity Multiplay/Relay, Steam, crossplay |
| Console build | likely Unity 2020.3.x IL2CPP ARM64 on Switch (verify) |

**Implication:** SBR 2 is the closer match to Marble Knights in shape
(same-generation Unity IL2CPP + an official Windows player), but much harder in
practice because of:

- Switch **NVN** graphics and shader path vs PC HDRP/D3D11;
- Nintendo SDK/NEX online services vs Steam/Unity Multiplay;
- Switch packaging/encryption and save formats;
- a far larger asset/platform surface;
- IL2CPP code that is optimized and partly stripped.

---

## 4. Feasibility matrix

| Goal | Approach | Feasibility | Effort |
| --- | --- | --- | --- |
| Play PC SBR 1/2 with mods on **Linux/Steam Deck** | Proton/Wine + BepInEx | **Already works** | Low |
| Play PC mods on **Windows handheld** | Same PC build | **Already works** | Low |
| **Native macOS** port | Recomp/translate PC x64; or source port | Hard | Very high |
| **Android/iOS** port | Recomp to ARM64 + Unity mobile player | Very hard | Very high |
| **Switch → PC** recomp | Marble Knights-style ARM64→C + PC Unity player + platform layer | Possible in principle | Very high / multi-year |
| **PS4/PS5 → PC** recomp | x86-64 translation + platform layer | Possible in principle | Very high |
| **PC source port of SBR 1** | Decompile Mono C#, rebuild Unity project | Plausible | High |
| **PC source port of SBR 2** | Cpp2IL decompile + rebuild Unity/HDRP project | Very hard | Extreme |
| **Mods on Switch/PS/Xbox** | Platform-specific CFW/jailbreak patching | Platform-dependent | High, separate project |

“Possible in principle” is doing a lot of work there. Marble Knights took a
specialist through an entire Unity IL2CPP runtime, a Darwin/mobile platform
layer and a Metal shader translator for one game.

---

## 5. Would recomp make mods portable?

**No, not by itself.**

Existing mods are not portable blobs that run on any build:

- **BepInEx 5/6 plugins** depend on the original `Assembly-CSharp`/`GameAssembly`
  method metadata/addresses, Unity runtime behavior, and PC platform APIs.
- A recompiled `GameAssembly` is a *different binary*. BepInEx/Il2CppInterop
  detours and IL2CPP metadata assumptions generally will not apply unchanged.
- Even if the recomp faithfully implements `il2cpp_*` entry points, the mod
  loader and each plugin would need to be validated/ported.
- **Console** targets need a loader/jailbreak and platform-specific injection,
  independent of the PC recomp.

What *is* portable:

1. **Data mods** — textures, audio, character assets, data-table patches — if
   the target build uses the same asset format and you can repack/sign it.
2. **Declarative feature settings** — custom BGM folder, story character key,
   post-processing toggles — if the target build has an adapter implementing
   them.
3. **A clean mod API** — if the recomp/source port exposes stable hooks, future
   mods can be written once and adapted per platform. Existing BepInEx mods
   would still need porting.

So the real portability layer is **not the recompiler**; it is a **mod manifest
+ platform adapter**.

---

## 6. Recommended plan

### Phase 0 — casual play + feature spec (now)

- Play SBR 1/2 casually.
- Write down the actual wanted features and which are data vs code:
  - data: music, textures, characters, UI, story tables;
  - code: skip movies, camera/FOV, post-processing, story character hook,
    custom modes.
- Do not start a recomp before this list exists.

### Phase 1 — finish the PC mod platform

- Ship/test the BepInEx 6 QoL plugin.
- Make character/audio/data mods declarative:
  - `mod.json` manifest (name, version, game, assets, patches, config);
  - external BGM folder + character keys;
  - asset replacement/repack pipeline.
- This is the part that can survive a future port.

### Phase 2 — SBR 1 source-port spike (only if preservation matters)

- Decompile `Assembly-CSharp.dll` with ILSpy/dnSpy.
- Use AssetRipper for scenes/prefabs/animations.
- Rebuild a minimal boot → title → one battle scene in Unity 5.6 or a modern
  Unity LTS.
- Measure: how many platform services, shaders, online calls, and scene
  dependencies are missing?
- This is a research spike, not a commitment.

### Phase 3 — SBR 2 recomp feasibility study (only if a specific target exists)

Before writing code, answer:

- Which build are we sourcing? (Switch ARM64 is the likely Marble Knights
  analogue; PS5 is x86-64 but a different platform layer.)
- Do we have an official Unity **2020.3.36f1** player for the target? (PC does;
  other targets differ.)
- What is the platform API surface? graphics (NVN/GNM/D3D), audio, input,
  saves, user/account, online.
- Can the shaders be translated? HDRP + Fuse on PC vs the console path is a
  major blocker.
- Is online required? If yes, the project is much larger.
- Legal model: own-copy only, no assets/keys distributed, clean-room code.

Expected outcome: a multi-person, multi-month/year project, not a side feature.

### Phase 4 — portable mod API

Define a small platform-neutral interface:

```
manifest.json
assets/        textures, audio, character replacements
patches/       data-table edits
config/        BGM folder, story character, QoL flags
```

Adapters:

- **PC BepInEx 6**: current plugin.
- **Recomp/source port**: native hooks in the port.
- **Console**: only if a loader exists; otherwise data-only.

Mods that only use this interface remain portable. Code mods that reach into
`GameAssembly`/`Assembly-CSharp` do not.

---

## 7. What I would do next

1. **Play the game.** Build the feature list from actual play.
2. **Finish/test the PC BepInEx 6 plugin** and make BGM/character/data mods
   declarative.
3. **Do not start SBR 2 recomp.** The cost/benefit is bad until there is a
   concrete target and a reason PC/Proton is insufficient.
4. If a port is still wanted, run the **SBR 1 Mono source-port spike** first —
   it is the cheapest way to learn how much Unity game code/assets/platform
   services actually need rebuilding.
5. Treat SBR 2 as a Marble Knights-scale research project only after the SBR 1
   spike and only with a clear target (e.g. Switch→PC preservation, not
   “mods on consoles”).

## 8. Legal/ethical notes

- Recomp projects should require the user’s own copy; no ROMs, keys, decrypted
  assets or game code in the repo.
- Decompiled code and extracted assets are still Konami copyright; distributing
  them is risky.
- Console modding may violate platform/online ToS; keep gameplay mods offline.
- The safe public artifact is the **tool/runtime code**, not the game data.
