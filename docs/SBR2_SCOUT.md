# Super Bomberman R 2 — modding scout

Scout date: 2026-11-22  
Target: Steam PC build, app **1913740**, installed at
`~/.local/share/Steam/steamapps/common/SUPER BOMBERMAN R2`  
Method: direct inspection of the installed build (not just web research).

---

## TL;DR

SBR 2 is **very moddable**, but the public scene is essentially empty.

- **Engine:** Unity **2020.3.36f1**, **IL2CPP** (x86-64), HDRP + HexaDrive's
  `Fuse` HDRP extension.
- **Data:** ~14 GB of Unity AssetBundles under
  `SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64`, plus a plain
  `Win64` AssetBundleManifest listing **2,705 bundles**.
- **Protection:** every payload bundle has its **first `0x100` bytes encrypted
  with AES-256-ECB**. Everything after `0x100` is plain Unity data. The key is
  compiled into the IL2CPP class `ResourceCryptoKey`.
- **Key (current build):**
  ```
  cbbca0477115dc4d5b9e93bb57b937b9
  2cb95fc9114af306ceb4085b03df5185
  ```
- **We cracked and verified it.** Decrypting the first `0x100` bytes produces a
  valid `UnityFS` header; `common_battle` parses into 9,079 Unity objects, and
  textures, audio clips and localisation text export cleanly.
- **No anti-cheat** (no EAC/BattlEye); only Steamworks. Online is Unity
  Multiplay/Relay with console crossplay, so gameplay mods online are a bad idea,
  but offline/private modding is unencumbered.
- **Code modding:** requires **BepInEx 6 (IL2CPP x64)** or **MelonLoader**.
  The existing BepInEx 5 x86 setup for SBR 1 will not work.
- **Public mods:** NexusMods has an empty SBR hub; GameBanana's SBR hub has
  ~8 subscribers and no files; the rest is trainers/cheat software. There is no
  public SBR 2 asset/code modding toolchain to build on, so this is greenfield.

Working tools added to this repo:

- `tools/sbr2_pack.py` — decrypt/encrypt/inspect bundles, `decrypt-all`
- `tools/extract_sbr2_assets.py` — export textures, sprites, WAVs, text/JSON

---

## 1. Installed build fingerprint

| Item | Value |
| --- | --- |
| Steam app | 1913740 (`SUPER BOMBERMAN R 2`) |
| Install size | 15,799,496,932 bytes |
| Steam buildid | `23462050` |
| Content `Version` file | `35199` |
| Unity | `2020.3.36f1` |
| Scripting | IL2CPP, metadata v27 |
| Executable | `SuperBombermanR2.exe` (x64) |
| IL2CPP binary | `GameAssembly.dll` (63.7 MB) |
| Render pipeline | HDRP + `FuseHDRenderPipelineAsset` |
| Anti-cheat | none found (no EAC/BattlEye/Denuvo) |
| Graphics API | Direct3D 11 |
| Save data | Proton prefix `.../AppData/LocalLow/Konami Digital Entertainment Co_, Ltd_/SUPER BOMBERMAN R 2/<steamid>/` |

The game ships a plain `Win64` manifest bundle (195,568 bytes) and 2,705 named
bundles. Bundle counts by top-level category:

| Category | Bundles | Category | Bundles |
| --- | ---: | --- | ---: |
| `map` | 1,331 | `cutscene` | 67 |
| `costume` | 373 | `stage_edit` | 53 |
| `grade` | 220 | `battlecamera` | 37 |
| `acce` | 195 | `movie` | 21 |
| `chara` | 134 | `boss_robot` | 10 |
| `playericon` | 105 | `vfx`, `story`, `common_ui` | 5 each |
| `sounds` | 96 | `defines`, `bomb`, `appeal`, etc. | few |

907 bundles declare dependencies on other bundles.

---

## 2. Public scene (what already exists)

| Source | Finding |
| --- | --- |
| NexusMods `superbombermanr` | Hub exists; search snippet reports **0 mods** |
| GameBanana SBR hub (7845) | 8 subscribers, **0 files**; old 5816 page is trashed/empty |
| ModEngine thread 2386 | Lists ~20 "mods" — trainers/cheats, not content mods |
| PLITCH / XMODhub / GameBuff | Trainers and cheat tables |
| Reddit / Steam forums | Occasional gameplay questions, no SBR 2 modding scene |
| GitHub | No SBR 2 asset/code modding projects surfaced |
| SBR 1 scene | Small; this repo's Bomber Launcher/BomberQoL is already one of the more advanced public tools |

Caveat: the web-search backend was unavailable during this scout, so the public
search used DuckDuckGo HTML, the GameBanana API and search snippets. A private
Discord scene could exist, but there is no obvious public SBR 2 toolchain.

**Implication:** there is no "install this mod loader and use these packs"
path for SBR 2. The first useful tools have to be built.

---

## 3. Asset format and crypto

### 3.1 Layout

```
SuperBombermanR2_Data/
  StreamingAssets/
    InAppAssets/Win64/
      Win64                     # plain UnityFS AssetBundleManifest
      common_battle             # 94 MB shared battle assets
      chara/chara_001           # 294 MB character assets
      locale.en-us              # localisation / subtitles
      map/env/...               # per-stage environment bundles
      sounds/se/btl_common      # 333 AudioClips
      costume/..., acce/..., grade/..., ...
```

There is no `Assembly-CSharp.dll`; SBR 2 is IL2CPP. The IL2CPP metadata
(`global-metadata.dat`) is **not** encrypted and method names are intact, which
makes reverse engineering much easier than a stripped/obfuscated build.

### 3.2 The encryption (SBR 2 vs SBR 1)

SBR 1 used a **whole-file** AES-128-CBC scheme:

- password `c4GXA8FT`, salt `t4ChYLcH`
- PBKDF2-HMAC-SHA1, 1103 iterations
- key/IV derived from the file name
- `RijndaelManaged`, CBC, zero padding

That is what `tools/sbr_pack.py` implements for SBR 1.

SBR 2 changed to a **header-only** scheme:

- AES-256-ECB, fixed key, no IV
- only the first `0x100` bytes are encrypted
- the rest of the bundle is plain Unity data
- plaintext `Win64` manifest is skipped

The code path is visible in IL2CPP:

- `ResourceCryptoKey.setTo(byte[] to)` — assembles a 32-byte key from obfuscated
  static byte fields
- `ResourceDecrypter.decrypt(...)` — calls `NonAllocAes.decryptByECB(...)`
- `NonAllocAes` — a small AES implementation with 128/192/256-bit key support

The key is build-specific. This key is for the current Steam build:

```
cbbca0477115dc4d5b9e93bb57b937b9
2cb95fc9114af306ceb4085b03df5185
```

### 3.3 Verification

- Decrypting only the first `0x100` bytes yields a valid `UnityFS` header for
  every payload bundle checked (full walk of 2,706 files; `Win64` is already
  plain).
- `common_battle` loads in UnityPy as **9,079 objects**: 2,392 GameObjects,
  2,392 Transforms, 1,320 ParticleSystems, 217 Texture2Ds, 164 Meshes, etc.
- `sounds/se/btl_common` loads as **333 AudioClips**; exported clips are PCM
  WAV (48 kHz, stereo).
- `locale.en-us` loads as MonoBehaviour subtitle tables with English text.
- A few bundles have block-info tables that extend past `0x100`; those tail
  bytes are plain and the bundles still load when only `0x100` bytes are
  decrypted. **Do not decrypt more than `0x100` bytes** or you will corrupt the
  start of the data section.

### 3.4 Repacking

1. Decrypt the bundle (`sbr2_pack.py decrypt`).
2. Edit/replace assets with UnityPy/UABEA/AssetStudio.
3. Save the bundle as a normal UnityFS file.
4. Encrypt the first `0x100` bytes (`sbr2_pack.py encrypt` or
   `encrypt-in-place`).
5. Replace the file in `InAppAssets/Win64` (back up the original).

No references to `AssetBundleManifest.GetAssetBundleHash` were found in the
IL2CPP dump; the game does use `GetAllAssetBundles`. That suggests the manifest
hashes are not verified, but **test a harmless texture swap before assuming
it**. If the game does verify, the manifest's per-bundle `Hash128` would need
updating too.

End-to-end test performed during the scout: the `Character_Lance_Illust`
texture in `chara/ui_illust/lance` was inverted with UnityPy, saved as a new
bundle, header-encrypted, decrypted again and reloaded. The result is a valid
`UnityFS` bundle and the pixel change survived, so the edit → save → re-encrypt
loop is sound.

---

## 4. Moddability matrix

| Area | Difficulty | Method / notes |
| --- | --- | --- |
| UI text, subtitles, localisation | **Easy** | `locale.*` bundles → MonoBehaviour arrays (`_subtitle`, locale keys). Repack or patch at runtime with `LocaleManager`. |
| Textures, icons, costumes | **Easy** | `Texture2D`/`Sprite`; 217 textures in `common_battle`, 373 costume bundles, 105 player icons. Decrypt → export PNG → edit → repack → re-encrypt. |
| Audio / BGM / SFX | **Easy–Medium** | 333 `AudioClip`s in `sounds/se/btl_common`, PCM WAV; some may be Vorbis/ADPCM. Replace with matching length/format. `SoundManager` has BGM workers. |
| Models / animations | **Medium** | 134 `chara` bundles (294 MB `chara_001`), meshes/materials/skeletons. Bone/rig compatibility and material/HDRP setup are the hard parts. |
| Maps / levels | **Hard** | 1,331 `map` bundles with scenes/prefabs. The in-game **stage editor** is the sane path for custom maps; direct scene editing is possible but brittle. |
| Gameplay / code | **Medium–Hard** | IL2CPP + BepInEx 6/MelonLoader; method names intact; no anti-cheat. Harmony patches are feasible, but you must port SBR 1 hooks to new classes. |
| Shaders / HDRP look | **Hard** | HDRP + `Fuse` custom pipeline; shader bundles. Not a first project. |
| New bundles / DLC / shop | **Hard / avoid** | Requires manifest edits plus whatever server-side release flags the shop uses. High risk of breaking the game. |
| Saves | **Easy–Medium** | Save files are plain binary (`GameData`, `StoryData`, `SystemData`, `UGCMapData_*`). No encryption observed; struct reverse-engineering required. |
| Online play | **Not recommended** | Crossplay with Switch/PS/Xbox; asset mods are client-side, gameplay mods can desync or be treated as cheating. Keep mods offline/private. |

---

## 5. Toolchain

### Asset work

| Tool | Use |
| --- | --- |
| `tools/sbr2_pack.py` | Decrypt/encrypt/inspect SBR 2 bundles |
| `tools/extract_sbr2_assets.py` | Export PNG/WAV/JSON from selected bundles |
| **UnityPy** | Scripted extraction and replacement |
| **AssetStudio** | GUI browsing/export after decryption |
| **AssetRipper** | Whole-bundle/project export |
| **UABEA (UABE Avalonia)** | Edit/replace assets and save bundles |
| **Blender** | Mesh edits; export back to Unity-compatible FBX/OBJ |
| **GIMP/Krita** | Texture edits |

Typical texture swap:

```sh
python3 tools/sbr2_pack.py decrypt \
  "$SBR2/SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64/common_battle" \
  /tmp/common_battle.unity3d

# open /tmp/common_battle.unity3d in UABEA, replace the Texture2D, save

python3 tools/sbr2_pack.py encrypt-in-place /tmp/common_battle.unity3d
cp /tmp/common_battle.unity3d \
  "$SBR2/SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64/common_battle"
```

(`encrypt-in-place` keeps a `.orig` backup. Keep a separate copy of the whole
`Win64` folder before bulk edits.)

### Code work

| Tool | Use |
| --- | --- |
| **Il2CppDumper** | `dump.cs`, method addresses, string literals — already run successfully (metadata v27) |
| **Cpp2IL** | Decompile IL2CPP methods to IL/C# for porting |
| **BepInEx 6 BE (x64 IL2CPP)** | Preferred runtime mod loader; bleeding-edge builds at <https://builds.bepinex.dev/projects/bepinex_be> |
| **MelonLoader 0.6.x (IL2CPP)** | Alternative; auto-generates interop assemblies |
| **HarmonyX** | Runtime patches |
| **dnSpy/ILSpy** | Inspect generated interop/`Assembly-CSharp`-equivalent assemblies |
| **RenderDoc** | HDRP frame debugging, if needed |

Important differences from SBR 1:

- SBR 1: Unity 5.6.5, **Mono x86**, `Assembly-CSharp.dll`, BepInEx 5 x86.
- SBR 2: Unity 2020.3.36f1, **IL2CPP x64**, `GameAssembly.dll`, BepInEx 6 x64
  IL2CPP or MelonLoader.
- The existing `install-bepinex.sh` cannot be reused as-is; it downloads
  BepInEx 5 x86.
- Proton still needs `WINEDLLOVERRIDES="winhttp=n,b" %command%` (or the Wine
  registry override) for BepInEx's doorstop.

Likely SBR 2 hook points found in the dump (names differ from SBR 1):

- `SplashLogo`, `EntryScene.LogoManage` — publisher/logo skip
- `MovieManager`, `UnityEngine.Video.VideoPlayer` — cutscene skip
- `SoundManager`, `AudioManager` — BGM/SFX replacement
- `LocaleManager` — text/voice locale
- `SaveDataManager`, `SaveGameData`, `SaveDataUpgrader` — saves
- `ResourceManager`, `ResourceDecrypter` — asset loading/decryption
- `Bloom`, `DepthOfField`, `MotionBlur`, `Vignette`, `FilmGrain` HDRP volume
  components — post-processing toggles
- `StageEdit*`, `MapLoader.UGCStageRequestServer` — stage editor/UGC

There is no `StageSetup` or `Practice` class with those exact names in SBR 2;
the SBR 1 practice-stage selector will need a new implementation.

---

## 6. Online, anti-cheat, and risk

- **No kernel anti-cheat.** Only Steamworks (`steam_api64.dll`). This is why
  trainers work and why code mods are technically easy.
- **Crossplay** with Switch, PS4/PS5, Xbox One/Series. PC-side asset mods are
  visible only to the modded client, but gameplay mods can desync or give an
  unfair advantage.
- **Unity Multiplay/Relay** (`Unity.Services.Multiplay`, `hx.nn.webapi`,
  `hx.socket`) handles online. There is no evidence of server-side file
  integrity checks, but do not assume online is safe for mods.
- **Steam Cloud** syncs saves; back them up.
- **Steam "Verify integrity"** restores vanilla bundles and removes mods.
- **Legal:** tools and patches are fine; do not redistribute Konami assets.
  This repo's existing policy (no game assets committed) should extend to SBR 2.

---

## 7. SBR 1 → SBR 2 porting notes

| | SBR 1 | SBR 2 |
| --- | --- | --- |
| Unity | 5.6.5 | 2020.3.36f1 |
| Scripting | Mono x86 | IL2CPP x64 |
| Managed code | `Assembly-CSharp.dll` | `GameAssembly.dll` + `global-metadata.dat` |
| Mod loader | BepInEx 5 x86 | BepInEx 6 IL2CPP x64 / MelonLoader |
| Data root | `StreamingAssets/InAppAssets/steam` | `StreamingAssets/InAppAssets/Win64` |
| Crypto | Full-file AES-128-CBC, PBKDF2 per filename | First `0x100` bytes AES-256-ECB, fixed key |
| Renderer | Built-in | HDRP + Fuse extension |
| Online | Separate/older online | Crossplay, Unity Multiplay |
| Save format | `savedata.bin` (xorshift32 + MD5 chunks) | Multiple plain binary files |
| Existing tool | `tools/sbr_pack.py`, BepInEx 5 installer | `tools/sbr2_pack.py`, BepInEx 6 installer (to add) |

The good news: SBR 2's protection is **weaker** than SBR 1's for asset work
(header-only ECB vs whole-file CBC), and the code is **less obfuscated**
(IL2CPP metadata names intact). The bad news: the runtime is a different
scripting backend, so the plugin must be rewritten rather than recompiled.

---

## 8. Recommended roadmap for Bomber Launcher

1. **Add an SBR 2 tab / game selector.**
   - Detect `SUPER BOMBERMAN R2/SuperBombermanR2.exe`.
   - Support `SBR2_GAME` and `SBR2_MODKIT` env vars.
   - Keep SBR 1 and SBR 2 settings/backups separate.

2. **Ship the asset tools.**
   - `tools/sbr2_pack.py` (already added).
   - `tools/extract_sbr2_assets.py` (already added).
   - Add a GUI action: "Decrypt SBR 2 packs", "Extract textures/audio",
     "Re-encrypt edited bundle".

3. **Add a BepInEx 6 x64 IL2CPP installer.**
   - Download the BepInEx 6 bleeding-edge x64 build (not BepInEx 5 x86).
   - Set the Proton `winhttp` override automatically.
   - Run once to generate interop assemblies, then install the plugin.

4. **Port QoL features incrementally.**
   - Logo/publisher skip (`SplashLogo`, `EntryScene.LogoManage`).
   - Cutscene skip (`MovieManager`, `VideoPlayer`).
   - Post-processing toggles (HDRP volume components).
   - Soft reset / relaunch.
   - BGM/SFX folders (`SoundManager`).
   - Language/voice switching (`LocaleManager`, locale bundles).
   - Stage select/practice only after mapping the SBR 2 stage flow.

5. **Add SBR 2 save backup/restore.**
   - Files: `GameData`, `StoryData`, `SystemData`, `KeyBoardConfigData`,
     `PadConfigData`, `QualityData`, `UGCMapData_*`, `UGCStageSettingData_*`.
   - Plain binary; start with backup/restore, then document structs.

6. **Document the "verify integrity" recovery path.**
   - Back up original bundles before bulk replacement.
   - Keep a manifest of edited files so the launcher can restore them.

### Distribution model

Prefer **local patch application** over redistributing modified bundles. A mod
pack can be a folder of PNG/WAV/JSON files plus a small manifest; the launcher
decrypts the user's own bundle, applies the replacements, and re-encrypts it.
That keeps downloads small and avoids shipping Konami assets. The `.orig`
backups plus Steam "Verify integrity" are the recovery path.

`encrypt` on a whole decrypted tree intentionally **skips the plain `Win64`
manifest**. If you deliberately edited the manifest, encrypt that one file with
`encrypt-in-place`.

---

## 9. Open questions / tests worth doing

1. **Hash/integrity check:** swap one harmless texture in `common_battle`,
   re-encrypt, and launch offline. If it loads, the manifest hash is not
   enforced. (No `GetAssetBundleHash` references found, so this is likely.)
2. **Audio codec coverage:** confirm which `AudioClip`s are PCM vs Vorbis/ADPCM
   before promising arbitrary audio replacement.
3. **Key stability:** re-check after each Steam update. If the key changes,
   re-run Il2CppDumper and replay `ResourceCryptoKey.setTo`, or hook
   `ResourceDecrypter.decrypt` at runtime.
4. **Manifest edits:** whether adding a new bundle name to `Win64` is enough,
   or whether shop/release flags are also required. Avoid until necessary.
5. **Online behavior:** test only in offline/private lobbies; do not bring
   gameplay mods into crossplay.
6. **Console versions:** file layout/key may differ (Switch/PS/Xbox). The PC
   build is the practical target.

---

## Appendix A — quick commands

```sh
# Inspect a bundle (works encrypted or decrypted)
python3 tools/sbr2_pack.py info \
  "$SBR2/SuperBombermanR2_Data/StreamingAssets/InAppAssets/Win64/common_battle"

# Decrypt one bundle
python3 tools/sbr2_pack.py decrypt \
  "$SBR2/.../Win64/common_battle" /tmp/common_battle.unity3d

# Decrypt the whole 14 GB payload tree
python3 tools/sbr2_pack.py decrypt-all /tmp/sbr2-bundles

# Encrypt an edited bundle (keeps .orig)
python3 tools/sbr2_pack.py encrypt-in-place /tmp/common_battle.unity3d

# Export 200 textures from the shared battle bundle
python3 tools/extract_sbr2_assets.py --kind texture --bundle common_battle

# Export all battle SFX
python3 tools/extract_sbr2_assets.py --kind audio \
  --bundle sounds/se/btl_common --limit 0

# Export localisation/subtitle MonoBehaviours
python3 tools/extract_sbr2_assets.py --kind text --bundle locale.en-us --limit 0
```

## Appendix B — sample extraction (verified)

From `common_battle`:

```
texture/mat_ob108_greenmisobomb_MaskMap.png
texture/tex_ob110_lightbluemisobomb_AL.png
texture/tex_map_ob004_pbtreasurebox_glass_MODS_G5.png
texture/tex_ob0002_penetrationbomb_AL.png
texture/tex_pp006_morningstar_chain_AL.png
```

From `sounds/se/btl_common`:

```
audio/sounds/se/SE_btl_special_damage_87.wav
audio/sounds/se/SE_btl_cmn_31.wav
audio/sounds/se/SE_btl_kasnei_count_08.wav
```

From `locale.en-us`:

```
text/Fuse2_09_stage2_OP.json
text/Fuse2_13_stage2_BO2.json
text/Fuse2_20_epilogue.json
```

## Appendix C — how the key was recovered (for future patches)

1. Run **Il2CppDumper** against `GameAssembly.dll` +
   `il2cpp_data/Metadata/global-metadata.dat` (metadata v27).
2. Find `ResourceCryptoKey.setTo(byte[] to)` (RVA `0x1B3F010`) and
   `ResourceDecrypter.decrypt` (RVA `0x1B3FBB0`).
3. `setTo` writes 32 bytes into `to` from 32 obfuscated static byte fields. Each
   field's `.cctor` does `mov byte ptr [static], imm8`.
4. Replay the 32 stores in `setTo` to get the key.
5. Verify by decrypting the first `0x100` bytes of a known bundle; the result
   must start with `UnityFS`.

If a future patch changes the key, the same procedure works, or hook
`ResourceDecrypter.decrypt` / `NonAllocAes.decryptByECB` under BepInEx 6 and log
the key at runtime.
