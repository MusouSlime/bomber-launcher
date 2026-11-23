using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.UI;

namespace BomberQoL
{
    internal enum PickupPoolMode
    {
        Vanilla,
        Expanded,
        Chaos,
        Difficulty
    }

    [BepInPlugin(GUID, "Bomber QoL", "0.6.7")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "bomber.qol";

        internal static ManualLogSource Log;
        internal static Plugin Instance;
        internal static ConfigEntry<bool> SkipEnabled;
        internal static ConfigEntry<string> SkipKeyName;
        internal static ConfigEntry<int> SkipJoystickButton;
        internal static ConfigEntry<int> SkipJoystickButtonAlt;
        internal static ConfigEntry<bool> AutoSkipLogos;
        internal static ConfigEntry<float> AutoSkipMovies;
        internal static ConfigEntry<bool> UnlockMaster;
        internal static ConfigEntry<bool> SoftResetEnabled;
        internal static ConfigEntry<string> SoftResetPadCombo;
        internal static ConfigEntry<float> SoftResetPadHold;
        internal static ConfigEntry<bool> SoftResetKeyEnabled;
        internal static ConfigEntry<string> SoftResetKeyName;
        internal static ConfigEntry<bool> SoftResetRequireCtrl;
        internal static ConfigEntry<bool> SoftResetSave;
        internal static ConfigEntry<bool> SoftResetDebug;
        internal static ConfigEntry<bool> SoftResetRelaunch;
        internal static ConfigEntry<string> PracticeMenuKeyName;
        internal static ConfigEntry<string> PracticeMenuPadCombo;
        internal static ConfigEntry<float> PracticeMenuPadHold;
        internal static ConfigEntry<bool> PracticeMenuDebug;
        internal static ConfigEntry<int> PracticeFire;
        internal static ConfigEntry<int> PracticeBomb;
        internal static ConfigEntry<int> PracticeSpeed;
        internal static ConfigEntry<int> PracticePunch;
        internal static ConfigEntry<int> PracticeGlove;
        internal static ConfigEntry<int> PracticeKick;
        internal static ConfigEntry<int> PracticeRemote;
        internal static ConfigEntry<int> PracticePenetration;
        internal static ConfigEntry<int> PracticeElasticity;
        internal static ConfigEntry<string> CustomBgmFolder;
        internal static ConfigEntry<string> CustomBgmSource;
        internal static ConfigEntry<bool> CustomBgmPlaylist;
        internal static ConfigEntry<float> CustomBgmVolume;
        internal static ConfigEntry<bool> CustomBgmShuffle;
        internal static ConfigEntry<string> CustomSfxFolder;
        internal static ConfigEntry<bool> CustomSfxShuffle;
        internal static ConfigEntry<float> CustomSfxVolume;
        internal static ConfigEntry<bool> RandomStoryCharacters;
        internal static ConfigEntry<bool> RandomMoveSets;
        internal static ConfigEntry<bool> RandomEnemies;
        internal static ConfigEntry<int> RandomizerSeed;
        internal static ConfigEntry<bool> SkipCharacterSelect;
        internal static ConfigEntry<bool> UnlockRoomAchievements;
        internal static ConfigEntry<bool> SmarterBots;
        internal static ConfigEntry<bool> RandomCpuOpponent;
        internal static ConfigEntry<bool> CpuSpecialAbilities;
        internal static ConfigEntry<bool> PlatformExclusives;
        internal static ConfigEntry<bool> MainMenuQoL;
        internal static ConfigEntry<bool> SkipGemContinue;
        internal static ConfigEntry<bool> MasterStatReset;
        internal static ConfigEntry<bool> ResetStatsPerStage;
        internal static ConfigEntry<bool> PracticeRepeatStage;
        internal static ConfigEntry<bool> RandomPickups;
        internal static ConfigEntry<int> RandomPickupCount;
        internal static ConfigEntry<PickupPoolMode> RandomPickupPool;
        internal static ConfigEntry<bool> HidePickupIcons;
        internal static ConfigEntry<bool> PickupToast;
        internal static ConfigEntry<bool> FastResults;
        internal static ConfigEntry<float> ResultSpeed;
        internal static ConfigEntry<bool> NoMotionBlur;
        internal static ConfigEntry<bool> NoDepthOfField;
        internal static ConfigEntry<bool> NoBloom;
        internal static ConfigEntry<bool> NoVignette;
        internal static ConfigEntry<bool> NoGrain;
        internal static KeyCode SkipKey = KeyCode.Space;
        internal static KeyCode SoftResetKey = KeyCode.F10;
        internal static KeyCode PracticeMenuKey = KeyCode.F9;
        internal static int[] SoftResetButtons = { 4, 5, 7 };
        internal static int[] PracticeMenuPadButtons = { 4, 5, 6 };
        internal static bool ForceMasterEnv;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ForceMasterEnv = Environment.GetEnvironmentVariable(
                "BOMBER_FORCE_MASTER") == "1";

            SkipEnabled = Config.Bind("Movies", "SkipEnabled", true,
                "Allow skipping cutscenes and the boot intro with a key.");
            SkipKeyName = Config.Bind("Movies", "SkipKey", "Space",
                "Unity KeyCode used to skip a playing movie (Space, Escape, ...).");
            SkipJoystickButton = Config.Bind("Movies", "SkipJoystickButton", 0,
                "Joystick button used to skip a playing movie: 0=A/Cross, 1=B/Circle, 2=X/Square, 3=Y/Triangle, 4=LB, 5=RB, 6=Back, 7=Start; -1 disables. Prefer A/Cross: the game's Submit-based skips ignore Cancel (B).");
            SkipJoystickButtonAlt = Config.Bind("Movies", "SkipJoystickButtonAlt", 1,
                "Secondary joystick button (default 1=B/Circle); -1 disables. Both buttons skip.");
            AutoSkipLogos = Config.Bind("Movies", "AutoSkipLogos", true,
                "Automatically skip publisher/logo animations (Konami, 33rd, HexaDrive).");
            AutoSkipMovies = Config.Bind("Movies", "AutoSkipMovies", 0f,
                "Automatically skip every movie after this many seconds (0 = off).");
            UnlockMaster = Config.Bind("Gameplay", "UnlockMaster", false,
                "Add the hidden Master difficulty button to the difficulty menu.");
            SoftResetEnabled = Config.Bind("Gameplay", "SoftResetEnabled", true,
                "Enable the soft reset (save and return to title without quitting).");
            SoftResetPadCombo = Config.Bind("Gameplay", "SoftResetPadCombo", "4,5,7",
                "Controller buttons to hold: 0=A/Cross, 1=B/Circle, 2=X/Square, 3=Y/Triangle, 4=LB/L1, 5=RB/R1, 6=Back/Share, 7=Start/Options. Default LB+RB+Start.");
            SoftResetPadHold = Config.Bind("Gameplay", "SoftResetPadHold", 0.75f,
                "Seconds the controller combo must be held (0.2 - 3.0).");
            SoftResetKeyEnabled = Config.Bind("Gameplay", "SoftResetKeyEnabled", true,
                "Also enable a keyboard soft-reset key (Ctrl+F10 by default).");
            SoftResetKeyName = Config.Bind("Gameplay", "SoftResetKey", "F10",
                "Unity KeyCode for the optional keyboard soft reset (F10, R, ...).");
            SoftResetRequireCtrl = Config.Bind("Gameplay", "SoftResetRequireCtrl", true,
                "Require Ctrl to be held with the optional keyboard soft-reset key.");
            SoftResetSave = Config.Bind("Gameplay", "SoftResetSave", false,
                "Save all data before soft resetting. Off by default; saving a story battle can write an invalid stock-0 save.");
            SoftResetDebug = Config.Bind("Gameplay", "SoftResetDebug", false,
                "Log controller button states while the soft-reset combo is held (diagnostics).");
            SoftResetRelaunch = Config.Bind("Gameplay", "SoftResetRelaunch", true,
                "Make the soft-reset combo restart the game process instead of returning to the title screen.");
            PracticeMenuKeyName = Config.Bind("Practice", "PracticeMenuKey", "F9",
                "Key used to open the in-game practice stage selector.");
            PracticeMenuPadCombo = Config.Bind("Practice", "PracticeMenuPadCombo", "4,5,2",
                "Controller combo for the practice selector: 0=A, 1=B, 2=X, 3=Y, 4=L1, 5=R1, 6=Select, 7=Start. Default L1+R1+X; Select is not exposed by this game.");
            PracticeMenuPadHold = Config.Bind("Practice", "PracticeMenuPadHold", 0.6f,
                "Seconds the practice-selector combo must be held (0.2 - 3.0).");
            PracticeMenuDebug = Config.Bind("Practice", "PracticeMenuDebug", false,
                "Log practice-selector button states while its combo is held (diagnostics).");
            PracticeFire = Config.Bind("Practice", "StatsFire", 3,
                "Custom practice fire-up count (index 1).");
            PracticeBomb = Config.Bind("Practice", "StatsBomb", 3,
                "Custom practice bomb-up count (index 4).");
            PracticeSpeed = Config.Bind("Practice", "StatsSpeed", 2,
                "Custom practice speed-up count (index 6).");
            PracticePunch = Config.Bind("Practice", "StatsPunch", 1,
                "Custom practice punch (index 8).");
            PracticeGlove = Config.Bind("Practice", "StatsGlove", 1,
                "Custom practice power glove (index 9).");
            PracticeKick = Config.Bind("Practice", "StatsKick", 1,
                "Custom practice kick (index 10).");
            PracticeRemote = Config.Bind("Practice", "StatsRemote", 0,
                "Custom practice remote (index 11).");
            PracticePenetration = Config.Bind("Practice", "StatsPenetration", 0,
                "Custom practice penetration (index 12).");
            PracticeElasticity = Config.Bind("Practice", "StatsElasticity", 0,
                "Custom practice elasticity (index 14).");
            CustomBgmFolder = Config.Bind("Audio", "CustomBgmFolder", "",
                "Folder containing custom .ogg/.wav BGM files. Empty disables replacement. The launcher converts MP3/FLAC folders into this folder.");
            CustomBgmSource = Config.Bind("Audio", "CustomBgmSource", "",
                "Original music folder chosen in the launcher (for display).");
            CustomBgmPlaylist = Config.Bind("Audio", "CustomBgmPlaylist", false,
                "If a BGM label has no matching file, cycle through the folder instead of using the original track.");
            CustomBgmVolume = Config.Bind("Audio", "CustomBgmVolume", 0.8f,
                "Volume for custom BGM (0.0 - 1.0).");
            CustomBgmShuffle = Config.Bind("Audio", "CustomBgmShuffle", true,
                "Shuffle the custom BGM folder when a BGM label has no exact file match.");
            CustomSfxFolder = Config.Bind("Audio", "CustomSfxFolder", "",
                "Folder with custom sound effects; files must be named <label>.ogg/wav or <label>_N.ogg/wav. Empty disables replacement.");
            CustomSfxShuffle = Config.Bind("Audio", "CustomSfxShuffle", true,
                "Randomize among custom SFX variants for the same sound label.");
            CustomSfxVolume = Config.Bind("Audio", "CustomSfxVolume", 0.8f,
                "Volume for custom sound effects (0.0 - 1.0).");
            RandomStoryCharacters = Config.Bind("Randomizer", "RandomStoryCharacters", false,
                "Randomize the story character selection at each StageSetup.");
            RandomEnemies = Config.Bind("Randomizer", "RandomEnemies", false,
                "Randomize the enemy types that spawn on story/practice maps (uses each map's enemy palette).");
            RandomMoveSets = Config.Bind("Randomizer", "RandomMoveSets", false,
                "Shuffle the A-button special ability on each character status; the normal B-button bomb stays intact.");
            RandomizerSeed = Config.Bind("Randomizer", "Seed", 0,
                "Seed for the randomizer (0 = random each launch).");
            SkipCharacterSelect = Config.Bind("Randomizer", "SkipCharacterSelect", true,
                "Skip StageSetup character select when random characters or a random run is active.");
            UnlockRoomAchievements = Config.Bind("Achievements", "UnlockRoomAchievements", false,
                "One-shot: call TrophyManager.FuncCreateRoom 100x on the next launch to unlock Room Creator/Master/Champ.");
            SmarterBots = Config.Bind("Bots", "SmarterBots", false,
                "Increase NPC search range and target re-evaluation speed for local battle bots.");
            RandomCpuOpponent = Config.Bind("Multiplayer", "RandomCpuOpponent", true,
                "Randomize the character used by CPU slots in local battle.");
            CpuSpecialAbilities = Config.Bind("Multiplayer", "CpuSpecialAbilities", true,
                "Let local battle CPUs use their A-button special ability when they have one.");
            PlatformExclusives = Config.Bind("Multiplayer", "PlatformExclusives", false,
                "Make the built-in platform-exclusive character IDs usable (Master Chief, Ratchet, etc.).");
            MainMenuQoL = Config.Bind("UI", "MainMenuQoL", true,
                "Replace the main menu Other option with Bomber QoL and open the F9 overlay.");
            SkipGemContinue = Config.Bind("Gameplay", "SkipGemContinue", true,
                "On story game over, skip the gem continue dialog and return to the main menu.");
            MasterStatReset = Config.Bind("Gameplay", "MasterStatReset", true,
                "On Master difficulty, reset the run to default starting stats.");
            ResetStatsPerStage = Config.Bind("Randomizer", "ResetStatsPerStage", true,
                "Apply the selected stats preset again at the start of every stage.");
            PracticeRepeatStage = Config.Bind("Practice", "RepeatStage", false,
                "In practice mode, restart the same stage after it is cleared.");
            RandomPickups = Config.Bind("Randomizer", "RandomPickups", true,
                "During a Random Run, move fixed map pickups into random breakable blocks and add extra random pickups.");
            RandomPickupCount = Config.Bind("Randomizer", "RandomPickupCount", 10,
                "Random Run pickups hidden in blocks per stage (0 = keep the map's vanilla count).");
            RandomPickupPool = Config.Bind("Randomizer", "RandomPickupPool", PickupPoolMode.Difficulty,
                "Random Run pickup pool: Vanilla, Expanded, Chaos, or Difficulty (Beginner all positive, Veteran mixed, Expert 50/50, Master 50/50 + stat reset).");
            HidePickupIcons = Config.Bind("Randomizer", "HidePickupIcons", true,
                "During a Random Run, show a blank pickup instead of the real item icon until it is collected.");
            PickupToast = Config.Bind("Randomizer", "PickupToast", true,
                "During a Random Run, show the collected pickup's name above the player.");
            FastResults = Config.Bind("Results", "FastResults", false,
                "Shorten the post-battle result screen delays.");
            ResultSpeed = Config.Bind("Results", "ResultSpeed", 0.5f,
                "Multiplier for result-screen intervals (0.1 - 1.0).");

            NoMotionBlur = Config.Bind("Graphics", "DisableMotionBlur", true,
                "Disable the motion blur post effect.");
            NoDepthOfField = Config.Bind("Graphics", "DisableDepthOfField", true,
                "Disable the depth of field post effect.");
            NoBloom = Config.Bind("Graphics", "DisableBloom", false,
                "Disable bloom.");
            NoVignette = Config.Bind("Graphics", "DisableVignette", false,
                "Disable the vignette.");
            NoGrain = Config.Bind("Graphics", "DisableGrain", false,
                "Disable film grain.");

            try
            {
                SkipKey = (KeyCode)Enum.Parse(typeof(KeyCode), SkipKeyName.Value, true);
            }
            catch
            {
                SkipKey = KeyCode.Space;
                Log.LogWarning("Unknown SkipKey, falling back to Space.");
            }

            try
            {
                SoftResetKey = (KeyCode)Enum.Parse(
                    typeof(KeyCode), SoftResetKeyName.Value, true);
            }
            catch
            {
                SoftResetKey = KeyCode.F10;
                Log.LogWarning("Unknown SoftResetKey, falling back to F10.");
            }

            try
            {
                PracticeMenuKey = (KeyCode)Enum.Parse(
                    typeof(KeyCode), PracticeMenuKeyName.Value, true);
            }
            catch
            {
                PracticeMenuKey = KeyCode.F9;
                Log.LogWarning("Unknown PracticeMenuKey, falling back to F9.");
            }

            SoftResetButtons = ParseSoftResetButtons(SoftResetPadCombo.Value);
            if (SoftResetButtons.Length == 0)
            {
                SoftResetButtons = new[] { 4, 5, 7 };
                Log.LogWarning("Invalid SoftResetPadCombo; using 4,5,7 (LB+RB+Start).");
            }

            PracticeMenuPadButtons = ParseSoftResetButtons(PracticeMenuPadCombo.Value);
            if (PracticeMenuPadButtons.Length == 0)
            {
                PracticeMenuPadButtons = new[] { 4, 5, 2 };
                Log.LogWarning("Invalid PracticeMenuPadCombo; using 4,5,2 (L1+R1+X).");
            }
            string combo = "";
            for (int i = 0; i < PracticeMenuPadButtons.Length; i++)
            {
                if (i > 0) combo += ",";
                combo += PracticeMenuPadButtons[i];
            }
            Log.LogInfo("Practice combo: " + combo +
                " hold " + PracticeMenuPadHold.Value + "s (buttons 4=L1,5=R1,2=X)");

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(PostFxPatch));
            harmony.PatchAll(typeof(InputAdapterSkipPatch));
            harmony.PatchAll(typeof(PracticeSavePatch));
            harmony.PatchAll(typeof(PracticeSaveAllPatch));
            harmony.PatchAll(typeof(PracticeInputPatch));
            harmony.PatchAll(typeof(SmarterBotsPatch));
            harmony.PatchAll(typeof(RandomCpuOpponentPatch));
            harmony.PatchAll(typeof(RandomCpuReadyResetPatch));
            harmony.PatchAll(typeof(RandomCpuFinalLogPatch));
            harmony.PatchAll(typeof(CpuSkillDesirePatch));
            harmony.PatchAll(typeof(CpuSkillConditionPatch));
            harmony.PatchAll(typeof(PlatformUsableCharacterPatch));
            harmony.PatchAll(typeof(PlatformUsableBuiltinPatch));
            harmony.PatchAll(typeof(MainMenuOptionPatch));
            harmony.PatchAll(typeof(RandomEnemiesPatch));
            harmony.PatchAll(typeof(MainMenuLabelPatch));
            harmony.PatchAll(typeof(CustomBgmPlayPatch));
            harmony.PatchAll(typeof(CustomBgmChangePatch));
            harmony.PatchAll(typeof(CustomBgmStopPatch));
            harmony.PatchAll(typeof(CustomBgmPausePatch));
            harmony.PatchAll(typeof(CustomBgmUnpausePatch));
            harmony.PatchAll(typeof(CustomBgmRestartPatch));
            harmony.PatchAll(typeof(CustomBgmPopPatch));
            harmony.PatchAll(typeof(CustomSfxPlayPatch));
            harmony.PatchAll(typeof(CustomSfxStopPatch));
            harmony.PatchAll(typeof(CustomSfxIsPlayingPatch));
            harmony.PatchAll(typeof(CustomSfxStopAllPatch));
            harmony.PatchAll(typeof(RandomCharacterPatch));
            harmony.PatchAll(typeof(RandomRunPatch));
            harmony.PatchAll(typeof(PickupMapPatch));
            harmony.PatchAll(typeof(PickupIconPatch));
            harmony.PatchAll(typeof(PickupToastPatch));
            harmony.PatchAll(typeof(GemContinuePatch));
            harmony.PatchAll(typeof(PracticeStatsPatch));

            Type logoType = AccessTools.Inner(typeof(TitleUI), "LogoState");
            MethodInfo logoExec = logoType == null ? null :
                AccessTools.Method(logoType, "execute");
            FieldInfo logoSkip = logoType == null ? null :
                AccessTools.Field(logoType, "_skippable");
            if (logoExec != null && logoSkip != null)
            {
                LogoSkipPatch.SkippableField = logoSkip;
                LogoSkipPatch.AnimField = AccessTools.Field(logoType, "_anim");
                LogoSkipPatch.SkippedField = AccessTools.Field(logoType, "_isSkiped");
                LogoSkipPatch.NameField = AccessTools.Field(logoType, "_name");
                harmony.Patch(logoExec, prefix: new HarmonyMethod(
                    typeof(LogoSkipPatch).GetMethod("Prefix",
                        BindingFlags.Static | BindingFlags.NonPublic)));
                Log.LogInfo("Logo skip patch applied");
            }
            else
            {
                Log.LogWarning("LogoState patch target not found");
            }

            MethodInfo titleInit = AccessTools.Method(typeof(TitleUI), "init",
                new[] { typeof(TitleScene), typeof(string), typeof(string) });
            FieldInfo logoInfos = AccessTools.Field(typeof(TitleUI), "_logoInfos");
            if (titleInit != null && logoInfos != null)
            {
                TitleLogosPatch.LogosField = logoInfos;
                harmony.Patch(titleInit, prefix: new HarmonyMethod(
                    typeof(TitleLogosPatch).GetMethod("Prefix",
                        BindingFlags.Static | BindingFlags.NonPublic)));
                Log.LogInfo("Title logo removal patch applied");
            }
            else
            {
                Log.LogWarning("TitleUI init patch target not found");
            }

            var watcher = new GameObject("BomberQoLSkipper");
            UnityEngine.Object.DontDestroyOnLoad(watcher);
            watcher.AddComponent<SkipWatcher>();
            var practiceMenu = new GameObject("BomberQoLPractice");
            UnityEngine.Object.DontDestroyOnLoad(practiceMenu);
            practiceMenu.AddComponent<PracticeMenu>();
            var customBgm = new GameObject("BomberQoLCustomBgm");
            UnityEngine.Object.DontDestroyOnLoad(customBgm);
            customBgm.AddComponent<CustomBgmPlayer>();
            var customSfx = new GameObject("BomberQoLCustomSfx");
            UnityEngine.Object.DontDestroyOnLoad(customSfx);
            customSfx.AddComponent<CustomSfxPlayer>();
            StartCoroutine(LogJoysticksSoon());

            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("Bomber QoL loaded (skip key: " + SkipKey +
                        ", pad button: " + SkipJoystickButton.Value + ")");
            Log.LogInfo("Soft reset: pad " + SoftResetPadCombo.Value +
                        " hold " + SoftResetPadHold.Value + "s; keyboard " +
                        (SoftResetKeyEnabled.Value ? "Ctrl+" + SoftResetKey : "off"));
            StartCoroutine(EnableMasterSoon());
        }

        private void Start()
        {
            ApplyAllPostFx();
            ApplyResultTweaks();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Practice.OnSceneLoaded(scene);
            Randomizer.EnsureBombNormalSkills();
            PlatformCharacters.ApplyLocaleNames();
            MainMenuQoLUtility.Apply();
            RandomRun.OnSceneLoaded(scene);
            ApplyAllPostFx();
            ApplyResultTweaks();
            EnableMasterButton();
        }

        private IEnumerator LogJoysticksSoon()
        {
            yield return new WaitForSeconds(2f);
            string[] names = Input.GetJoystickNames();
            Log.LogInfo("Joysticks visible to Unity: " +
                (names == null || names.Length == 0
                    ? "(none)" : string.Join(", ", names)));
        }

        private IEnumerator EnableMasterSoon()
        {
            yield return new WaitForSeconds(0.5f);
            EnableMasterButton();
        }

        internal static void EnableMasterButton()
        {
            if (!UnlockMaster.Value) return;
            UnityEngine.Object[] all =
                Resources.FindObjectsOfTypeAll(typeof(GameObject));
            int enabled = 0;
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i] as GameObject;
                if (go == null || go.name != "SuperButton") continue;
                if (!go.scene.IsValid()) continue;
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                    enabled++;
                }
                Button button = go.GetComponent<Button>();
                if (button != null) button.interactable = true;
            }
            WireMasterNavigation();
            WireMasterClick();
            if (enabled > 0)
                Log.LogInfo("Master difficulty button enabled (" +
                            enabled + ")");
        }

        internal static bool TryForceMaster()
        {
            if (!ForceMasterEnv) return false;
            if (FindWorldMapUI() == null) return false;
            StoryManager sm = SingletonBase<StoryManager>.getInstance();
            if (sm == null) return false;
            DifficultyDataTable.DifficultyData data = sm.findDifficulty("super");
            if (data == null) return false;
            sm.setCurrentDifficulty(data);
            Log.LogInfo("Forced Master difficulty via BOMBER_FORCE_MASTER");
            return true;
        }

        private static Button FindUiButton(string name)
        {
            UnityEngine.Object[] all =
                Resources.FindObjectsOfTypeAll(typeof(GameObject));
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i] as GameObject;
                if (go == null || go.name != name || !go.scene.IsValid())
                    continue;
                Button b = go.GetComponent<Button>();
                if (b != null) return b;
            }
            return null;
        }

        private static WorldMapUI FindWorldMapUI()
        {
            UnityEngine.Object[] all =
                Resources.FindObjectsOfTypeAll(typeof(WorldMapUI));
            for (int i = 0; i < all.Length; i++)
            {
                WorldMapUI ui = all[i] as WorldMapUI;
                if (ui != null && ui.isActiveAndEnabled) return ui;
            }
            return null;
        }

        private static void WireMasterClick()
        {
            Button master = FindUiButton("SuperButton");
            if (master == null) return;
            master.onClick.RemoveAllListeners();
            master.onClick.AddListener(() =>
            {
                WorldMapUI ui = FindWorldMapUI();
                if (ui != null) ui.closeDialog(3);
            });
            Log.LogInfo("Master difficulty click wired");
        }

        private static void WireMasterNavigation()
        {
            Button easy = FindUiButton("EasyButton");
            Button hard = FindUiButton("HardButton");
            Button master = FindUiButton("SuperButton");
            if (easy == null || hard == null || master == null) return;

            Navigation hn = hard.navigation;
            hn.mode = Navigation.Mode.Explicit;
            hn.selectOnDown = master;
            hard.navigation = hn;

            Navigation en = easy.navigation;
            en.mode = Navigation.Mode.Explicit;
            en.selectOnUp = master;
            easy.navigation = en;

            Navigation mn = master.navigation;
            mn.mode = Navigation.Mode.Explicit;
            mn.selectOnUp = hard;
            mn.selectOnDown = easy;
            master.navigation = mn;
        }

        internal static void ApplyAllPostFx()
        {
            UnityEngine.Object[] all =
                Resources.FindObjectsOfTypeAll(typeof(PostProcessingBehaviour));
            for (int i = 0; i < all.Length; i++)
            {
                PostProcessingBehaviour b = all[i] as PostProcessingBehaviour;
                if (b != null) PostFxPatch.Apply(b);
            }
        }

        private static readonly HashSet<int> ScaledResults = new HashSet<int>();
        private static readonly string[] ResultFields =
        {
            "_postLoadInterval", "_postRemainingInterval", "_postCountInterval",
            "_postSpecialInterval", "_postNoContinueInterval", "_postTotalInterval",
            "_postAddInterval", "_postEvaluateInterval"
        };

        internal static void ApplyResultTweaks()
        {
            if (!FastResults.Value) return;
            UnityEngine.Object[] all =
                Resources.FindObjectsOfTypeAll(typeof(WorldResultDefData));
            for (int i = 0; i < all.Length; i++)
            {
                WorldResultDefData d = all[i] as WorldResultDefData;
                if (d != null) ScaleResult(d);
            }
        }

        private static void ScaleResult(WorldResultDefData data)
        {
            int id = data.GetInstanceID();
            if (ScaledResults.Contains(id)) return;
            ScaledResults.Add(id);
            float speed = ResultSpeed.Value;
            if (speed <= 0f) speed = 0.1f;
            if (speed > 1f) speed = 1f;
            Type type = data.GetType();
            int changed = 0;
            for (int i = 0; i < ResultFields.Length; i++)
            {
                FieldInfo f = type.GetField(ResultFields[i],
                    BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public);
                if (f == null || f.FieldType != typeof(float)) continue;
                float v = (float)f.GetValue(data);
                if (v <= 0f) continue;
                f.SetValue(data, v * speed);
                changed++;
            }
            if (changed > 0)
                Log.LogInfo("Result timings scaled by " + speed +
                            " (" + changed + " fields)");
        }

        private static int[] ParseSoftResetButtons(string text)
        {
            if (string.IsNullOrEmpty(text)) return new int[0];
            string[] parts = text.Split(new[] { ',', '+', ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new List<int>();
            for (int i = 0; i < parts.Length; i++)
            {
                int button;
                if (!int.TryParse(parts[i].Trim(), out button)) continue;
                if (button < 0 || button > 19) continue;
                if (!result.Contains(button)) result.Add(button);
            }
            return result.ToArray();
        }

        private static string PadButtonName(int button)
        {
            switch (button)
            {
                case 0: return "A";
                case 1: return "B";
                case 2: return "X";
                case 3: return "Y";
                case 4: return "L";
                case 5: return "R";
                case 6: return "SELECT";
                case 7: return "START";
                default: return null;
            }
        }

        internal static bool PadButtonHeld(int button)
        {
            if (button < 0 || button > 19) return false;

            // Use the game's InputAdapter so Steam Input / remapped pads work.
            string name = PadButtonName(button);
            if (name != null)
            {
                InputAdapter adapter =
                    SingletonBaseWithGameObject<InputAdapter>.getInstance();
                if (adapter != null)
                {
                    for (int p = 1; p <= 8; p++)
                    {
                        try
                        {
                            if (adapter.GetButton(name + "_" + p + "P"))
                                return true;
                        }
                        catch { }
                    }
                }
            }

            // Raw Unity fallback for buttons not exposed by InputAdapter.
            if (Input.GetKey((KeyCode)(330 + button))) return true;
            for (int joy = 1; joy <= 8; joy++)
            {
                if (Input.GetKey(
                    (KeyCode)(350 + (joy - 1) * 20 + button)))
                    return true;
            }
            return false;
        }

        private static bool SoftResetBlocked()
        {
            string scene = SceneManager.GetActiveScene().name;
            return scene == "Reset" || scene == "Entry";
        }

        internal static bool PracticeDebugHeld()
        {
            if (PracticeMenuPadButtons == null) return false;
            for (int i = 0; i < PracticeMenuPadButtons.Length; i++)
            {
                if (PadButtonHeld(PracticeMenuPadButtons[i])) return true;
            }
            return false;
        }

        internal static void LogPracticePadState()
        {
            string raw = "";
            if (PracticeMenuPadButtons != null)
            {
                for (int i = 0; i < PracticeMenuPadButtons.Length; i++)
                {
                    raw += PracticeMenuPadButtons[i] + "=" +
                        (PadButtonHeld(PracticeMenuPadButtons[i]) ? "1" : "0") + " ";
                }
            }
            string adapt = "";
            InputAdapter adapter =
                SingletonBaseWithGameObject<InputAdapter>.getInstance();
            if (adapter != null)
            {
                string[] names = { "A", "B", "X", "Y", "L", "R", "START", "SELECT" };
                for (int i = 0; i < names.Length; i++)
                {
                    try
                    {
                        if (adapter.GetButton(names[i] + "_1P"))
                            adapt += names[i] + " ";
                    }
                    catch { }
                }
            }
            Log.LogInfo("Practice combo state: " + raw.Trim() +
                " adapter=[" + adapt.Trim() + "]");
        }

        internal static bool PracticeComboHeld()
        {
            if (PracticeMenuPadButtons == null ||
                PracticeMenuPadButtons.Length == 0)
                return false;
            for (int i = 0; i < PracticeMenuPadButtons.Length; i++)
            {
                if (!PadButtonHeld(PracticeMenuPadButtons[i])) return false;
            }
            return true;
        }

        internal static bool SoftResetComboHeld()
        {
            if (!SoftResetEnabled.Value || SoftResetBlocked()) return false;
            if (SoftResetButtons == null || SoftResetButtons.Length == 0)
                return false;
            for (int i = 0; i < SoftResetButtons.Length; i++)
            {
                if (!PadButtonHeld(SoftResetButtons[i])) return false;
            }
            return true;
        }

        internal static bool SoftResetKeyPressed()
        {
            if (!SoftResetEnabled.Value || !SoftResetKeyEnabled.Value ||
                SoftResetBlocked())
                return false;
            if (SoftResetRequireCtrl.Value &&
                !Input.GetKey(KeyCode.LeftControl) &&
                !Input.GetKey(KeyCode.RightControl))
                return false;
            return Input.GetKeyDown(SoftResetKey);
        }

        private static bool RawPadButtonHeld(int button)
        {
            if (button < 0 || button > 19) return false;
            if (Input.GetKey((KeyCode)(330 + button))) return true;
            for (int joy = 1; joy <= 8; joy++)
            {
                if (Input.GetKey(
                    (KeyCode)(350 + (joy - 1) * 20 + button)))
                    return true;
            }
            return false;
        }

        internal static bool SoftResetDebugHeld()
        {
            for (int b = 0; b < 20; b++)
            {
                if (RawPadButtonHeld(b)) return true;
            }
            InputAdapter adapter =
                SingletonBaseWithGameObject<InputAdapter>.getInstance();
            if (adapter == null) return false;
            string[] names = { "A", "B", "X", "Y", "L", "R", "START", "SELECT" };
            for (int i = 0; i < names.Length; i++)
            {
                for (int p = 1; p <= 8; p++)
                {
                    try
                    {
                        if (adapter.GetButton(names[i] + "_" + p + "P"))
                            return true;
                    }
                    catch { }
                }
            }
            return false;
        }

        internal static void LogSoftResetPadState()
        {
            string raw = "";
            for (int b = 0; b < 20; b++)
            {
                if (RawPadButtonHeld(b)) raw += b + " ";
            }
            string adapt = "";
            InputAdapter adapter =
                SingletonBaseWithGameObject<InputAdapter>.getInstance();
            if (adapter != null)
            {
                string[] names = { "A", "B", "X", "Y", "L", "R", "START", "SELECT" };
                for (int i = 0; i < names.Length; i++)
                {
                    for (int p = 1; p <= 8; p++)
                    {
                        try
                        {
                            if (adapter.GetButton(names[i] + "_" + p + "P"))
                            {
                                adapt += names[i] + ":" + p + " ";
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }
            Log.LogInfo("Soft reset debug: raw=[" + raw.Trim() +
                "] adapter=[" + adapt.Trim() + "]");
        }

        private static string QuoteArg(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static bool RelaunchProcess()
        {
            try
            {
                string dataPath = Application.dataPath;
                string gameDir = Path.GetDirectoryName(dataPath);
                string exe = Path.Combine(gameDir, "SuperBombermanR.exe");
                if (!File.Exists(exe))
                {
                    Log.LogWarning("Relaunch: executable not found at " + exe);
                    return false;
                }

                string args = "";
                string[] argv = Environment.GetCommandLineArgs();
                for (int i = 1; i < argv.Length; i++)
                {
                    if (args.Length > 0) args += " ";
                    args += QuoteArg(argv[i]);
                }

                string batch = Path.Combine(Path.GetTempPath(),
                    "bomber_relaunch.bat");
                string body =
                    "@echo off\r\n" +
                    "ping -n 3 127.0.0.1 >nul\r\n" +
                    "start \"\" \"" + exe + "\"" +
                    (args.Length > 0 ? " " + args : "") + "\r\n" +
                    "del \"%~f0\" >nul 2>&1\r\n";
                File.WriteAllText(batch, body);

                ProcessStartInfo psi = new ProcessStartInfo(
                    "cmd.exe", "/c \"\"" + batch + "\"\"");
                psi.WorkingDirectory = gameDir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
                Log.LogInfo("Relaunching game process (2s delay)");
                Application.Quit();
                return true;
            }
            catch (Exception e)
            {
                Log.LogWarning("Relaunch failed: " + e.Message);
                return false;
            }
        }

        internal static void DoSoftReset(string source)
        {
            if (SoftResetRelaunch.Value)
            {
                Log.LogInfo("Full relaunch via " + source);
                if (RelaunchProcess()) return;
                Log.LogWarning("Falling back to title reset");
            }

            bool save = SoftResetSave.Value;
            if (save)
            {
                StoryManager story = SingletonBase<StoryManager>.getInstance();
                if (story != null && story.playerStockNum == 0)
                {
                    save = false;
                    Log.LogWarning("Soft reset: skipping save because stock is 0");
                }
            }
            // ResetScene.Start() saves when this flag is true. Force it both
            // ways: a stale true from the current scene was still saving the
            // stock-0 battle state even when SoftResetSave was false.
            SceneBase.ResetSaveExecute = save;
            Log.LogInfo("Soft reset via " + source +
                " (save: " + save + ")");
            try
            {
                ResetUtility.resetStart();
            }
            catch (Exception e)
            {
                Log.LogWarning("Reset scene failed: " + e.Message);
                try
                {
                    SceneManager.LoadScene("Entry");
                }
                catch (Exception e2)
                {
                    Log.LogWarning("Entry fallback failed: " + e2.Message);
                }
            }
        }

        internal static bool SkipPressed(out string source)
        {
            if (Input.GetKeyDown(SkipKey))
            {
                source = SkipKey.ToString();
                return true;
            }
            int primary = SkipJoystickButton.Value;
            if (primary >= 0 && CheckButton(primary, out source))
                return true;
            int alt = SkipJoystickButtonAlt.Value;
            if (primary >= 0 && alt >= 0 && alt != primary &&
                CheckButton(alt, out source))
                return true;
            source = null;
            return false;
        }

        private static bool CheckButton(int button, out string source)
        {
            source = null;
            if (button < 0 || button > 19) return false;
            if (Input.GetKeyDown((KeyCode)(330 + button)))
            {
                source = "joystick button " + button;
                return true;
            }
            for (int joy = 1; joy <= 8; joy++)
            {
                if (Input.GetKeyDown(
                    (KeyCode)(350 + (joy - 1) * 20 + button)))
                {
                    source = "joystick button " + button +
                             " (pad " + joy + ")";
                    return true;
                }
            }
            return false;
        }

        internal static bool SkipVideo(VideoPlayer video, string source)
        {
            if (video == null || !video.isPlaying) return false;
            if (video.frameRate <= 0f || video.frameCount == 0) return false;
            double target = (double)video.frameCount / video.frameRate - 0.05;
            if (target < 0.0) target = 0.0;
            if (video.time >= target) return false;
            video.time = target;
            string name = video.clip != null ? video.clip.name : video.url;
            Log.LogInfo("Skipping movie via " + source + " [" + name + "]");
            return true;
        }

        private static float _movieCheckTime;
        private static bool _movieActive;

        internal static bool MovieActive()
        {
            if (Time.unscaledTime - _movieCheckTime < 0.2f)
                return _movieActive;
            _movieCheckTime = Time.unscaledTime;
            _movieActive = false;
            UnityEngine.Object[] videos =
                Resources.FindObjectsOfTypeAll(typeof(VideoPlayer));
            for (int i = 0; i < videos.Length; i++)
            {
                VideoPlayer v = videos[i] as VideoPlayer;
                if (v != null && (v.isPlaying || v.isPrepared))
                {
                    _movieActive = true;
                    return true;
                }
            }
            UnityEngine.Object[] scenes =
                Resources.FindObjectsOfTypeAll(typeof(AbstractMoviePlayScene));
            for (int i = 0; i < scenes.Length; i++)
            {
                AbstractMoviePlayScene s = scenes[i] as AbstractMoviePlayScene;
                if (s != null && s.isActiveAndEnabled)
                {
                    _movieActive = true;
                    return true;
                }
            }
            return false;
        }
    }

    public class CustomSfxPlayer : MonoBehaviour
    {
        internal static CustomSfxPlayer Instance;
        private class Voice
        {
            internal AudioSource source;
            internal int id;
        }

        private readonly List<Voice> _voices = new List<Voice>();
        private readonly Dictionary<string, AudioClip> _cache =
            new Dictionary<string, AudioClip>();
        private readonly HashSet<int> _ids = new HashSet<int>();
        private string _folder;
        private string[] _files;
        private int _nextId = -100000;

        private void Awake()
        {
            Instance = this;
        }

        private static string ResolveFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (folder.StartsWith("/"))
                folder = "Z:" + folder.Replace('/', '\\');
            return folder;
        }

        private static string ToFileUri(string path)
        {
            string p = path.Replace('\\', '/');
            return new Uri("file:///" + p).AbsoluteUri;
        }

        private static AudioType AudioTypeFor(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ogg") return AudioType.OGGVORBIS;
            if (ext == ".wav") return AudioType.WAV;
            if (ext == ".mp3") return AudioType.MPEG;
            return AudioType.UNKNOWN;
        }

        private string FileFor(string label)
        {
            string folder = ResolveFolder(Plugin.CustomSfxFolder.Value);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;
            if (folder != _folder)
            {
                _folder = folder;
                string[] all;
                try
                {
                    all = Directory.GetFiles(folder, "*",
                        SearchOption.AllDirectories);
                }
                catch
                {
                    all = Directory.GetFiles(folder);
                }
                var decodable = new List<string>();
                var compressed = new List<string>();
                for (int i = 0; i < all.Length; i++)
                {
                    string ext = Path.GetExtension(all[i]).ToLowerInvariant();
                    if (ext == ".ogg" || ext == ".wav")
                        decodable.Add(all[i]);
                    else if (ext == ".mp3")
                        compressed.Add(all[i]);
                }
                if (decodable.Count == 0) decodable = compressed;
                decodable.Sort(StringComparer.OrdinalIgnoreCase);
                _files = decodable.ToArray();
                Plugin.Log.LogInfo("Custom SFX: " + _files.Length +
                    " files (" + decodable.Count + " direct, " +
                    compressed.Count + " mp3) in " + folder);
            }
            if (_files == null || _files.Length == 0) return null;

            string want = Path.GetFileNameWithoutExtension(label);
            var matches = new List<string>();
            for (int i = 0; i < _files.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(_files[i]);
                if (string.Equals(name, want, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(want + "_", StringComparison.OrdinalIgnoreCase))
                    matches.Add(_files[i]);
            }
            if (matches.Count == 0) return null;
            if (Plugin.CustomSfxShuffle.Value && matches.Count > 1)
                return matches[UnityEngine.Random.Range(0, matches.Count)];
            return matches[0];
        }

        private Voice AcquireVoice()
        {
            for (int i = 0; i < _voices.Count; i++)
            {
                if (!_voices[i].source.isPlaying)
                    return _voices[i];
            }
            Voice v = new Voice();
            GameObject go = new GameObject("BomberQoLSfxVoice");
            go.transform.SetParent(transform, false);
            v.source = go.AddComponent<AudioSource>();
            v.source.playOnAwake = false;
            v.source.loop = false;
            v.source.spatialBlend = 0f;
            _voices.Add(v);
            return v;
        }

        internal static bool TryPlay(SoundManager owner, string label,
            float delay, out int id)
        {
            id = 0;
            if (Instance == null) return false;
            if (string.IsNullOrEmpty(Plugin.CustomSfxFolder.Value)) return false;
            string file = Instance.FileFor(label);
            if (file == null) return false;
            id = Instance._nextId--;
            Instance._ids.Add(id);
            Instance.StartCoroutine(Instance.Play(file, id, delay));
            return true;
        }

        private IEnumerator Play(string file, int id, float delay)
        {
            AudioClip clip;
            if (!_cache.TryGetValue(file, out clip))
            {
                WWW www = new WWW(ToFileUri(file));
                yield return www;
                if (!string.IsNullOrEmpty(www.error))
                {
                    Plugin.Log.LogWarning("Custom SFX failed: " + file +
                        " (" + www.error + ")");
                    www.Dispose();
                    _ids.Remove(id);
                    yield break;
                }
                clip = www.GetAudioClip(false, false, AudioTypeFor(file));
                if (clip == null)
                    clip = www.GetAudioClip(false, true, AudioTypeFor(file));
                if (clip == null)
                    clip = www.GetAudioClip(false, false);
                if (clip == null)
                    clip = www.GetAudioClip(false, true);
                www.Dispose();
                if (clip == null)
                {
                    Plugin.Log.LogWarning("Custom SFX decode failed: " + file +
                        " (try .ogg or .wav if this is an .mp3)");
                    _ids.Remove(id);
                    yield break;
                }
                _cache[file] = clip;
            }
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (!_ids.Contains(id)) yield break;
            Voice v = AcquireVoice();
            v.id = id;
            v.source.clip = clip;
            v.source.volume = Mathf.Clamp01(Plugin.CustomSfxVolume.Value);
            v.source.loop = false;
            v.source.Play();
        }

        internal static bool Stop(int id)
        {
            if (Instance == null) return false;
            if (!Instance._ids.Remove(id)) return false;
            for (int i = 0; i < Instance._voices.Count; i++)
            {
                if (Instance._voices[i].id != id) continue;
                Instance._voices[i].source.Stop();
                return true;
            }
            return true;
        }

        internal static bool IsPlaying(int id, out bool playing)
        {
            playing = false;
            if (Instance == null || !Instance._ids.Contains(id)) return false;
            for (int i = 0; i < Instance._voices.Count; i++)
            {
                if (Instance._voices[i].id != id) continue;
                playing = Instance._voices[i].source.isPlaying;
                return true;
            }
            return true;
        }

        internal static void StopAll()
        {
            if (Instance == null) return;
            for (int i = 0; i < Instance._voices.Count; i++)
                Instance._voices[i].source.Stop();
            Instance._ids.Clear();
        }
    }

    [HarmonyPatch(typeof(SoundManager), "play")]
    internal static class CustomSfxPlayPatch
    {
        private static bool Prefix(SoundManager __instance, string soundLabel,
            float delay, ref int __result)
        {
            int id;
            if (CustomSfxPlayer.TryPlay(__instance, soundLabel, delay, out id))
            {
                __result = id;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "stop")]
    internal static class CustomSfxStopPatch
    {
        private static bool Prefix(int instanceId)
        {
            return !CustomSfxPlayer.Stop(instanceId);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "isPlaying")]
    internal static class CustomSfxIsPlayingPatch
    {
        private static bool Prefix(int instanceId, ref bool __result)
        {
            bool playing;
            if (CustomSfxPlayer.IsPlaying(instanceId, out playing))
            {
                __result = playing;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "stopAll")]
    internal static class CustomSfxStopAllPatch
    {
        private static bool Prefix()
        {
            CustomSfxPlayer.StopAll();
            return true;
        }
    }

    public class CustomBgmPlayer : MonoBehaviour
    {
        internal static CustomBgmPlayer Instance;
        internal static bool Bypass;
        private AudioSource _source;
        private string _folder;
        private string[] _files;
        private int _playlistIndex;
        private Coroutine _loading;
        private SoundManager _owner;
        private string _label;
        private float _fade;
        private bool _change;

        private void Awake()
        {
            Instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        internal static bool HasActive
        {
            get
            {
                return Instance != null && Instance._source != null &&
                    Instance._loading == null &&
                    Instance._source.clip != null &&
                    Instance._source.gameObject.activeInHierarchy;
            }
        }

        internal static bool HasCustom
        {
            get
            {
                return Instance != null && Instance._source != null &&
                    (Instance._loading != null ||
                     Instance._source.clip != null);
            }
        }

        private static string ResolveFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (folder.StartsWith("/"))
                folder = "Z:" + folder.Replace('/', '\\');
            return folder;
        }

        private static string ToFileUri(string path)
        {
            string p = path.Replace('\\', '/');
            return new Uri("file:///" + p).AbsoluteUri;
        }

        private static AudioType AudioTypeFor(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".ogg") return AudioType.OGGVORBIS;
            if (ext == ".wav") return AudioType.WAV;
            if (ext == ".mp3") return AudioType.MPEG;
            return AudioType.UNKNOWN;
        }

        private static bool IsBgmLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            return label.StartsWith("BGM_", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("snd_bgm_", StringComparison.OrdinalIgnoreCase);
        }

        private string FileFor(string label)
        {
            string folder = ResolveFolder(Plugin.CustomBgmFolder.Value);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;
            if (folder != _folder)
            {
                _folder = folder;
                string[] all;
                try
                {
                    all = Directory.GetFiles(folder, "*",
                        SearchOption.AllDirectories);
                }
                catch
                {
                    all = Directory.GetFiles(folder);
                }
                var decodable = new List<string>();
                var compressed = new List<string>();
                for (int i = 0; i < all.Length; i++)
                {
                    string ext = Path.GetExtension(all[i]).ToLowerInvariant();
                    if (ext == ".ogg" || ext == ".wav")
                        decodable.Add(all[i]);
                    else if (ext == ".mp3" || ext == ".flac" ||
                             ext == ".m4a" || ext == ".opus")
                        compressed.Add(all[i]);
                }
                decodable.Sort(StringComparer.OrdinalIgnoreCase);
                _files = decodable.ToArray();
                _playlistIndex = 0;
                _bag = null;
                _bagPos = 0;
                Plugin.Log.LogInfo("Custom BGM: " + _files.Length +
                    " playable tracks, " + compressed.Count +
                    " compressed files skipped in " + folder);
                if (compressed.Count > 0)
                    Plugin.Log.LogWarning(
                        "Custom BGM: MP3/FLAC files need conversion; " +
                        "choose the folder in the launcher to build the OGG cache.");
            }
            if (_files == null || _files.Length == 0) return null;

            string want = Path.GetFileNameWithoutExtension(label);
            for (int i = 0; i < _files.Length; i++)
            {
                if (string.Equals(
                    Path.GetFileNameWithoutExtension(_files[i]), want,
                    StringComparison.OrdinalIgnoreCase))
                    return _files[i];
            }
            return null;
        }

        private string NextPlaylistFile()
        {
            if (_files == null || _files.Length == 0) return null;
            string file = _files[_playlistIndex % _files.Length];
            _playlistIndex = (_playlistIndex + 1) % _files.Length;
            return file;
        }

        private readonly System.Random _rng = new System.Random(
            unchecked(Environment.TickCount * 397) ^
            Guid.NewGuid().GetHashCode());
        private int[] _bag;
        private int _bagPos;
        private int _lastRandom = -1;

        private void ShuffleBag()
        {
            int n = _files.Length;
            if (_bag == null || _bag.Length != n) _bag = new int[n];
            for (int i = 0; i < n; i++) _bag[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int t = _bag[i];
                _bag[i] = _bag[j];
                _bag[j] = t;
            }
            _bagPos = 0;
            if (n > 1 && _bag[0] == _lastRandom)
            {
                int t = _bag[0];
                _bag[0] = _bag[n - 1];
                _bag[n - 1] = t;
            }
        }

        private string RandomFile()
        {
            if (_files == null || _files.Length == 0) return null;
            if (_files.Length == 1) return _files[0];
            if (_bag == null || _bagPos >= _bag.Length) ShuffleBag();
            int idx = _bag[_bagPos++];
            _lastRandom = idx;
            return _files[idx];
        }

        internal static bool TryPlay(SoundManager owner, string label,
            float fade, bool change)
        {
            if (Bypass || Instance == null) return false;
            if (string.IsNullOrEmpty(Plugin.CustomBgmFolder.Value)) return false;
            string file = Instance.FileFor(label);
            if (file == null)
            {
                if (!IsBgmLabel(label)) return false;
                if (Plugin.CustomBgmShuffle.Value)
                    file = Instance.RandomFile();
                else if (Plugin.CustomBgmPlaylist.Value)
                    file = Instance.NextPlaylistFile();
                else
                    return false;
            }
            if (file == null) return false;

            Instance._owner = owner;
            Instance._label = label;
            Instance._fade = fade;
            Instance._change = change;
            if (Instance._loading != null)
                Instance.StopCoroutine(Instance._loading);
            Instance._loading = Instance.StartCoroutine(
                Instance.LoadAndPlay(file));
            return true;
        }

        private IEnumerator LoadAndPlay(string file)
        {
            WWW www = new WWW(ToFileUri(file));
            yield return www;
            if (!string.IsNullOrEmpty(www.error))
            {
                Plugin.Log.LogWarning("Custom BGM failed: " + file + " (" +
                    www.error + ")");
                www.Dispose();
                _loading = null;
                Fallback();
                yield break;
            }
            AudioClip clip = www.GetAudioClip(false, false,
                AudioTypeFor(file));
            if (clip == null)
                clip = www.GetAudioClip(false, true, AudioTypeFor(file));
            if (clip == null)
                clip = www.GetAudioClip(false, false);
            if (clip == null)
                clip = www.GetAudioClip(false, true);
            www.Dispose();
            if (clip == null)
            {
                Plugin.Log.LogWarning("Custom BGM decode failed: " + file +
                    " (try .ogg or .wav if this is an .mp3)");
                _loading = null;
                Fallback();
                yield break;
            }
            _source.clip = clip;
            _source.volume = Mathf.Clamp01(Plugin.CustomBgmVolume.Value);
            _source.loop = true;
            _source.Play();
            Plugin.Log.LogInfo("Custom BGM: " + _label + " -> " +
                Path.GetFileName(file));
            _loading = null;
        }

        private void Fallback()
        {
            if (_source != null) _source.Stop();
            if (_owner == null) return;
            Bypass = true;
            try
            {
                if (_change) _owner.changeBGM(_label, _fade);
                else _owner.playAsBGM(_label, _fade);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("BGM fallback failed: " + e.Message);
            }
            finally
            {
                Bypass = false;
            }
        }

        internal static void Stop()
        {
            if (Instance == null) return;
            if (Instance._loading != null)
            {
                Instance.StopCoroutine(Instance._loading);
                Instance._loading = null;
            }
            if (Instance._source == null) return;
            Instance._source.Stop();
            Instance._source.clip = null;
        }

        internal static void Pause()
        {
            if (Instance != null && Instance._source != null)
                Instance._source.Pause();
        }

        internal static void Unpause()
        {
            if (Instance != null && Instance._source != null)
                Instance._source.UnPause();
        }

        internal static void Restart()
        {
            if (Instance != null && Instance._source != null &&
                Instance._source.clip != null)
            {
                Instance._source.Stop();
                Instance._source.Play();
            }
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playAsBGM")]
    internal static class CustomBgmPlayPatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            float fadeTime)
        {
            if (CustomBgmPlayer.Bypass) return true;
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, false);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "changeBGM")]
    internal static class CustomBgmChangePatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            float fadeTime)
        {
            if (CustomBgmPlayer.Bypass) return true;
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, true);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "stopBGM")]
    internal static class CustomBgmStopPatch
    {
        private static bool Prefix()
        {
            if (CustomBgmPlayer.Bypass || !CustomBgmPlayer.HasCustom)
                return true;
            CustomBgmPlayer.Stop();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "onPauseBGM")]
    internal static class CustomBgmPausePatch
    {
        private static bool Prefix()
        {
            if (CustomBgmPlayer.Bypass || !CustomBgmPlayer.HasActive)
                return true;
            CustomBgmPlayer.Pause();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "offPauseBGM")]
    internal static class CustomBgmUnpausePatch
    {
        private static bool Prefix()
        {
            if (CustomBgmPlayer.Bypass || !CustomBgmPlayer.HasActive)
                return true;
            CustomBgmPlayer.Unpause();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "restartBGM")]
    internal static class CustomBgmRestartPatch
    {
        private static bool Prefix()
        {
            if (CustomBgmPlayer.Bypass || !CustomBgmPlayer.HasActive)
                return true;
            CustomBgmPlayer.Restart();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "popBGM")]
    internal static class CustomBgmPopPatch
    {
        private static bool Prefix()
        {
            if (CustomBgmPlayer.Bypass || !CustomBgmPlayer.HasCustom)
                return true;
            CustomBgmPlayer.Stop();
            return false;
        }
    }

    internal static class Randomizer
    {
        private static System.Random _rng;
        private static bool _moveSetsApplied;
        private static int _seed;

        internal static int Next(int min, int max)
        {
            return Rng.Next(min, max);
        }

        private static System.Random Rng
        {
            get
            {
                if (_rng == null)
                {
                    _seed = Plugin.RandomizerSeed.Value;
                    if (_seed == 0) _seed = Environment.TickCount;
                    _rng = new System.Random(_seed);
                    Plugin.Log.LogInfo("Randomizer seed: " + _seed);
                }
                return _rng;
            }
        }

        private static int PickUsableIndex(CharacterDataManager cd,
            int count, int exclude)
        {
            for (int attempt = 0; attempt < 80; attempt++)
            {
                int index = Rng.Next(0, count);
                if (index == exclude) continue;
                CharacterInfoDataTable.CharacterInfoData info =
                    cd.getCharacterInfoFromSorted(index);
                if (info == null) continue;
                if (!cd.isUsableCharacter(info)) continue;
                return index;
            }
            return -1;
        }

        internal static void RandomizeCharacters()
        {
            if (!Plugin.RandomStoryCharacters.Value) return;
            // Practice uses the normal character-select flow. Only a Random
            // Run or normal story mode may randomize characters.
            if (Practice.Active && !RandomRun.Active) return;
            SaveDataManager sd =
                SingletonBaseWithGameObject<SaveDataManager>.getInstance();
            CharacterDataManager cd =
                SingletonBase<CharacterDataManager>.getInstance();
            if (sd == null || sd.storyData == null || cd == null) return;

            int count = cd.getTotalCharacterCount();
            if (count <= 0) return;
            int first = PickUsableIndex(cd, count, -1);
            if (first < 0) return;
            CharacterInfoDataTable.CharacterInfoData a =
                cd.getCharacterInfoFromSorted(first);
            if (a == null) return;
            CharacterDataManager.CharacterID idA =
                CharacterDataManager.getCharacterId(a);
            sd.storyData.characterDatas[0].characterId.set(
                (sbyte)idA.contentType, idA.characterId);

            GameSceneManager gs =
                SingletonBaseWithGameObject<GameSceneManager>.getInstance();
            MoveSceneParam mp = gs == null ? default(MoveSceneParam) : gs.param;
            if (mp.characterParams == null || mp.characterParams.Length < 2)
                mp.characterParams = new MoveSceneParam.CharacterParam[2];
            mp.characterParams[0].controlType = (BattleDef.CONTROL_TYPE)1;
            mp.characterParams[0].characterId = idA;
            mp.characterParams[0].statusId = a.normalStatusId;

            if (sd.storyData.characterDatas.Length > 1)
            {
                int second = PickUsableIndex(cd, count, first);
                CharacterInfoDataTable.CharacterInfoData b =
                    second >= 0 ? cd.getCharacterInfoFromSorted(second) : null;
                if (b != null)
                {
                    CharacterDataManager.CharacterID idB =
                        CharacterDataManager.getCharacterId(b);
                    sd.storyData.characterDatas[1].characterId.set(
                        (sbyte)idB.contentType, idB.characterId);
                    if (mp.characterParams != null &&
                        mp.characterParams.Length > 1)
                    {
                        mp.characterParams[1].characterId = idB;
                        mp.characterParams[1].statusId = b.normalStatusId;
                    }
                }
            }
            if (gs != null) gs.param = mp;
            Plugin.Log.LogInfo("Randomizer: story characters " +
                a.charKey + " (player slot initialized)" +
                (sd.storyData.characterDatas.Length > 1
                    ? ", rerolled P2" : ""));
        }

        internal static void ApplyMoveSets(bool force = false)
        {
            if (!Plugin.RandomMoveSets.Value) return;
            if (_moveSetsApplied && !force) return;
            CharacterDataManager cd =
                SingletonBase<CharacterDataManager>.getInstance();
            if (cd == null) return;
            CharacterStatusDataTable table = cd.getCharacterStatuses();
            if (table == null) return;
            Type t = typeof(CharacterStatusDataTable.CharacterStatusData);
            FieldInfo normal = t.GetField("_normalSkillType",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo special = t.GetField("_specialSkillType",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (normal == null || special == null)
            {
                Plugin.Log.LogWarning("Randomizer: skill fields not found");
                _moveSetsApplied = true;
                return;
            }

            int n = table.characterStatusDataCount;
            for (int i = 0; i < n; i++)
            {
                object data = table.getCharacterStatusData(i);
                if (data == null) continue;
                // The normal slot is always NORMAL_BOMB in the game data.
                // Randomizing it removed bomb placement, so only the special
                // slot is shuffled.
                normal.SetValue(data, Player.SKILL_TYPE.NORMAL_BOMB);
                special.SetValue(data, (Player.SKILL_TYPE)Rng.Next(1, 29));
            }
            _moveSetsApplied = true;
            Plugin.Log.LogInfo("Randomizer: shuffled move sets for " +
                n + " status entries (normal bomb kept, A-button specials shuffled)");
        }

        internal static void EnsureBombNormalSkills()
        {
            if (!Plugin.RandomMoveSets.Value) return;
            CharacterDataManager cd =
                SingletonBase<CharacterDataManager>.getInstance();
            if (cd == null) return;
            CharacterStatusDataTable table = cd.getCharacterStatuses();
            if (table == null) return;
            Type t = typeof(CharacterStatusDataTable.CharacterStatusData);
            FieldInfo normal = t.GetField("_normalSkillType",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (normal == null) return;

            int fixedCount = 0;
            int n = table.characterStatusDataCount;
            for (int i = 0; i < n; i++)
            {
                object data = table.getCharacterStatusData(i);
                if (data == null) continue;
                Player.SKILL_TYPE current =
                    (Player.SKILL_TYPE)normal.GetValue(data);
                if (current == Player.SKILL_TYPE.NORMAL_BOMB) continue;
                normal.SetValue(data, Player.SKILL_TYPE.NORMAL_BOMB);
                fixedCount++;
            }
            if (fixedCount > 0)
                Plugin.Log.LogInfo("Randomizer: repaired " + fixedCount +
                    " non-bomb normal skills");
        }
    }

    internal static class PlatformCharacters
    {
        private static readonly HashSet<string> ExclusiveKeys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "M001", "M002", "M003", "V001", "V002",
                "S001", "S002", "S003", "N001", "N002"
            };

        private static readonly HashSet<string> ExclusiveReferences =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "refM001", "refM002", "refM003", "refV001", "refV002",
                "refS001", "refS002", "refS003", "refN001", "refN002"
            };

        private static readonly FieldInfo LocaleDatasField =
            AccessTools.Field(typeof(LocaleDataTable), "_localeDatas");

        private static readonly Dictionary<string, string> LocaleNames =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "PF03_M001_BOMBER", "Master Chief Bomber" },
                { "PF03_M001_BOMBER_ABILITY", "Spartan Shot" },
                { "PF03_M002_BOMBER", "Arbiter Bomber" },
                { "PF03_M002_BOMBER_ABILITY", "Mediate" },
                { "PF03_M003_BOMBER", "Grunt Bomber" },
                { "PF03_M003_BOMBER_ABILITY", "Plasma Pistol" },
                { "PF02_S001_BOMBER", "Ratchet & Clank Bomber" },
                { "PF02_S001_BOMBER_ABILITY", "Ratchet Blaster" },
                { "PF02_S002_BOMBER", "Toro" },
                { "PF02_S002_BOMBER_ABILITY", "Copycat" },
                { "PF02_S003_BOMBER", "Piposaru" },
                { "PF02_S003_BOMBER_ABILITY", "Ape Escape" },
                { "PF01_N001_BOMBER", "Max" },
                { "PF01_N002_BOMBER", "Caesar Bomber" },
                { "PF01_N002_BOMBER_ABILITY", "Speedster" },
                { "PF04_V001_BOMBER", "P-body Bomber" },
                { "PF04_V001_BOMBER_ABILITY", "Portal #1" },
                { "PF04_V002_BOMBER", "ATLAS Bomber" },
                { "PF04_V002_BOMBER_ABILITY", "Portal #0" }
            };

        internal static bool IsExclusive(string key)
        {
            return !string.IsNullOrEmpty(key) && ExclusiveKeys.Contains(key);
        }

        internal static bool IsExclusiveReference(string key)
        {
            return !string.IsNullOrEmpty(key) &&
                ExclusiveReferences.Contains(key);
        }

        internal static void ApplyLocaleNames()
        {
            if (!Plugin.PlatformExclusives.Value) return;
            if (LocaleDatasField == null) return;
            LocaleManager lm = SingletonBase<LocaleManager>.getInstance();
            if (lm == null) return;
            LocaleDataTable table = lm.tbl;
            if (table == null) return;
            LocaleDataTable.LocaleData[] data =
                LocaleDatasField.GetValue(table) as
                    LocaleDataTable.LocaleData[];
            if (data == null) return;

            var missing = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> pair in LocaleNames)
            {
                object indexObj = LocaleDef.LOCALE_TABLE[pair.Key];
                if (indexObj is int)
                {
                    int index = (int)indexObj;
                    if (index >= 0 && index < data.Length &&
                        data[index] != null &&
                        data[index].val == pair.Value)
                        continue;
                }
                missing.Add(pair);
            }
            if (missing.Count == 0) return;

            LocaleDataTable.LocaleData[] expanded =
                new LocaleDataTable.LocaleData[data.Length + missing.Count];
            Array.Copy(data, expanded, data.Length);
            int at = data.Length;
            for (int i = 0; i < missing.Count; i++)
            {
                expanded[at] = new LocaleDataTable.LocaleData(
                    missing[i].Value);
                LocaleDef.LOCALE_TABLE[missing[i].Key] = at;
                at++;
            }
            LocaleDatasField.SetValue(table, expanded);
            Plugin.Log.LogInfo("Platform exclusives: injected " +
                missing.Count + " locale names");
        }
    }

    internal static class MainMenuQoLUtility
    {
        private static readonly FieldInfo LocaleDatasField =
            AccessTools.Field(typeof(LocaleDataTable), "_localeDatas");
        private static string _originalMenu;
        private static string _originalCaption;
        private static bool _captured;
        private static bool _logged;

        internal static void Apply()
        {
            if (LocaleDatasField == null) return;
            LocaleManager lm = SingletonBase<LocaleManager>.getInstance();
            if (lm == null) return;
            LocaleDataTable table = lm.tbl;
            if (table == null) return;
            LocaleDataTable.LocaleData[] data =
                LocaleDatasField.GetValue(table) as
                    LocaleDataTable.LocaleData[];
            if (data == null) return;
            int menuIndex = (int)LocaleDef.MAINMENU.MENU_OTHER;
            int captionIndex = (int)LocaleDef.MAINMENU.CAPTION_OTHER;
            if (menuIndex < 0 || captionIndex < 0 ||
                menuIndex >= data.Length || captionIndex >= data.Length)
                return;

            if (!_captured)
            {
                _originalMenu = data[menuIndex] == null
                    ? null : data[menuIndex].val;
                _originalCaption = data[captionIndex] == null
                    ? null : data[captionIndex].val;
                _captured = true;
            }

            if (!Plugin.MainMenuQoL.Value)
            {
                data[menuIndex] = new LocaleDataTable.LocaleData(
                    _originalMenu ?? "Other");
                data[captionIndex] = new LocaleDataTable.LocaleData(
                    _originalCaption ?? string.Empty);
                LocaleDatasField.SetValue(table, data);
                return;
            }

            data[menuIndex] = new LocaleDataTable.LocaleData("Bomber QoL");
            data[captionIndex] = new LocaleDataTable.LocaleData(
                "Practice, randomizer, multiplayer and settings");
            LocaleDatasField.SetValue(table, data);
            if (!_logged)
            {
                _logged = true;
                Plugin.Log.LogInfo(
                    "Main menu: replaced Other with Bomber QoL");
            }
        }
    }

    internal static class Pickups
    {
        private const string MarkName = "BomberQoL_ItemMark";

        private static readonly FieldInfo MapDataField =
            AccessTools.Field(typeof(AbstractBattleSceneBase), "_mapData");
        private static readonly FieldInfo ItemInfosField =
            AccessTools.Field(typeof(AbstractBattleSceneBase), "_itemInfos");
        private static readonly FieldInfo ComBattleHdlField =
            AccessTools.Field(typeof(AbstractBattleSceneBase), "_comBattleResHdl");
        private static readonly FieldInfo RandomItemMinField =
            AccessTools.Field(typeof(RuntimeMapData), "_randomItemMin");
        private static readonly FieldInfo RandomItemMaxField =
            AccessTools.Field(typeof(RuntimeMapData), "_randomItemMax");
        private static readonly FieldInfo ModelGoField =
            AccessTools.Field(typeof(Item.Model), "_go");

        private static Font _font;
        private static bool _fontTried;
        private static Sprite _blankSprite;

        internal static bool Active
        {
            get
            {
                return (RandomRun.Active || Practice.Active) &&
                    Plugin.RandomPickups.Value;
            }
        }

        internal static Font UiFont
        {
            get
            {
                if (_font != null || _fontTried) return _font;
                _fontTried = true;
                try
                {
                    LocaleManager lm = SingletonBase<LocaleManager>.getInstance();
                    if (lm != null) _font = lm.font;
                }
                catch { }
                if (_font == null)
                {
                    try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch { }
                }
                if (_font == null)
                {
                    try { _font = Font.CreateDynamicFontFromOSFont("Arial", 64); }
                    catch { }
                }
                if (_font == null)
                    Plugin.Log.LogWarning("Pickups: no font available for labels");
                return _font;
            }
        }

        private static string LocaleText(LocaleDef.COMMON id)
        {
            try
            {
                LocaleManager lm = SingletonBase<LocaleManager>.getInstance();
                if (lm == null) return null;
                LocaleDataTable tbl = lm.tbl;
                if (tbl == null) return null;
                LocaleDataTable.LocaleData d =
                    LocaleDataTableExtension.getLocaleData(tbl, id);
                return d == null ? null : d.val;
            }
            catch { return null; }
        }

        private static Sprite BlankSprite
        {
            get
            {
                if (_blankSprite != null) return _blankSprite;
                const int size = 64;
                Texture2D tex = new Texture2D(size, size,
                    TextureFormat.ARGB32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                Color edge = new Color(1f, 0.82f, 0.35f, 1f);
                Color center = new Color(1f, 0.98f, 0.88f, 1f);
                float radius = size * 0.46f;
                float mid = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - mid;
                        float dy = y - mid;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((radius - d) / 3f);
                        Color col = new Color(0f, 0f, 0f, 0f);
                        if (a > 0f)
                        {
                            float t = Mathf.Clamp01(
                                (radius - d) / (radius * 0.45f));
                            col = Color.Lerp(edge, center, t);
                            col.a = a;
                        }
                        tex.SetPixel(x, y, col);
                    }
                }
                tex.Apply();
                _blankSprite = Sprite.Create(tex,
                    new Rect(0f, 0f, size, size),
                    new Vector2(0.5f, 0.5f), size);
                _blankSprite.name = "BomberQoL_BlankPickup";
                return _blankSprite;
            }
        }

        internal static string ItemName(ItemTypedef.TYPE type)
        {
            LocaleDef.COMMON id = LocaleDef.COMMON.ITEM_QUESTIONMARK;
            bool localized = true;
            switch (type)
            {
                case ItemTypedef.TYPE.FIRE_UP:
                    id = LocaleDef.COMMON.ITEM_FIREUP; break;
                case ItemTypedef.TYPE.FIRE_DOWN:
                    id = LocaleDef.COMMON.ITEM_FIREDOWN; break;
                case ItemTypedef.TYPE.FIRE_FULL:
                    id = LocaleDef.COMMON.ITEM_FULLFIRE; break;
                case ItemTypedef.TYPE.BOMB_UP:
                    id = LocaleDef.COMMON.ITEM_BOMBUP; break;
                case ItemTypedef.TYPE.BOMB_DOWN:
                    id = LocaleDef.COMMON.ITEM_BOMBDOWN; break;
                case ItemTypedef.TYPE.SPEED_UP:
                    id = LocaleDef.COMMON.ITEM_SPEEDUP; break;
                case ItemTypedef.TYPE.SPEED_DOWN:
                    id = LocaleDef.COMMON.ITEM_SPEEDDOWN; break;
                case ItemTypedef.TYPE.PUNCH:
                    id = LocaleDef.COMMON.ITEM_PUNCH; break;
                case ItemTypedef.TYPE.POWER_GLOVE:
                    id = LocaleDef.COMMON.ITEM_POWERGLOVE; break;
                case ItemTypedef.TYPE.KICK:
                    id = LocaleDef.COMMON.ITEM_KICK; break;
                case ItemTypedef.TYPE.PENETRATION:
                    id = LocaleDef.COMMON.ITEM_PENETRATINGBOMB; break;
                case ItemTypedef.TYPE.ELASTICITY:
                    id = LocaleDef.COMMON.ITEM_BOUNCINGBOMB; break;
                case ItemTypedef.TYPE.SKULL:
                    id = LocaleDef.COMMON.ITEM_SKULL; break;
                default:
                    localized = false; break;
            }
            if (localized)
            {
                string text = LocaleText(id);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            switch (type)
            {
                case ItemTypedef.TYPE.FIRE_UP: return "Fire Up";
                case ItemTypedef.TYPE.FIRE_DOWN: return "Fire Down";
                case ItemTypedef.TYPE.FIRE_FULL: return "Full Fire";
                case ItemTypedef.TYPE.BOMB_UP: return "Bomb Up";
                case ItemTypedef.TYPE.BOMB_DOWN: return "Bomb Down";
                case ItemTypedef.TYPE.SPEED_UP: return "Speed Up";
                case ItemTypedef.TYPE.SPEED_DOWN: return "Speed Down";
                case ItemTypedef.TYPE.PUNCH: return "Punch";
                case ItemTypedef.TYPE.POWER_GLOVE: return "Power Glove";
                case ItemTypedef.TYPE.KICK: return "Kick";
                case ItemTypedef.TYPE.REMOTE: return "Remote";
                case ItemTypedef.TYPE.PENETRATION: return "Penetration Bomb";
                case ItemTypedef.TYPE.SKULL: return "Skull";
                case ItemTypedef.TYPE.ELASTICITY: return "Bouncing Bomb";
                case ItemTypedef.TYPE.KEY: return "Key";
                case ItemTypedef.TYPE.CRYSTAL: return "Crystal";
                default: return type.ToString();
            }
        }

        private static uint PositiveWeight(int type)
        {
            if (type == 1 || type == 4 || type == 6) return 12;
            if (type == 8 || type == 9 || type == 10) return 6;
            if (type == 3 || type == 12 || type == 14) return 3;
            return 0;
        }

        private static uint DifficultyWeight(int type, float negativeChance)
        {
            uint positive = PositiveWeight(type);
            if (positive > 0) return positive;
            if (negativeChance <= 0f) return 0;
            // Positive weight total is 3*12 + 3*6 + 3*3 = 63.
            uint negativeTotal = (uint)Mathf.Round(63f * negativeChance /
                Mathf.Max(0.01f, 1f - negativeChance));
            if (negativeTotal <= 0) return 0;
            uint down = (uint)Mathf.Round(negativeTotal / 3.5f);
            if (down < 1) down = 1;
            if (type == 2 || type == 5 || type == 7) return down;
            if (type == 13) return (uint)Mathf.Max(1, (int)down / 2);
            return 0;
        }

        private static uint Weight(PickupPoolMode pool, int type,
            float negativeChance)
        {
            if (pool == PickupPoolMode.Chaos)
            {
                if (type == 1 || type == 4 || type == 6) return 10;
                if (type == 8 || type == 9 || type == 10) return 5;
                if (type == 3 || type == 12 || type == 14) return 4;
                if (type == 2 || type == 5 || type == 7) return 2;
                if (type == 13) return 1;
                return 0;
            }
            if (pool == PickupPoolMode.Expanded)
                return PositiveWeight(type);
            if (pool == PickupPoolMode.Difficulty)
                return DifficultyWeight(type, negativeChance);
            return 0;
        }

        private static void ExpandPool(AbstractBattleSceneBase scene,
            ItemController.ItemInfoTable infos, float negativeChance)
        {
            ResourceManager rm =
                SingletonBaseWithGameObject<ResourceManager>.getInstance();
            IHandle handle = ComBattleHdlField.GetValue(scene) as IHandle;
            if (rm == null || handle == null) return;
            ItemDataTable data =
                rm.getAsset<ItemDataTable>(handle, "ItemDataTable");
            if (data == null) return;
            PickupPoolMode pool = Plugin.RandomPickupPool.Value;
            for (int i = 0; i < data.itemDataCount; i++)
            {
                ItemDataTable.ItemData item = data.getItemData(i);
                if (item == null) continue;
                int type = (int)item.itemType;
                if (type <= 0 || type > 14) continue;
                ItemController.ItemInfo info;
                if (!infos.TryGetValue(type, out info))
                {
                    GameObject prefab =
                        rm.getAsset<GameObject>(handle, item.prefabName);
                    if (prefab == null) continue;
                    info = new ItemController.ItemInfo(item.itemType,
                        item.isPositive, prefab, 0);
                }
                info.appearanceRatioWeight =
                    Weight(pool, type, negativeChance);
                infos[type] = info;
            }
        }

        private static string CurrentDifficulty()
        {
            StoryManager story = SingletonBase<StoryManager>.getInstance();
            if (story == null) return "normal";
            DifficultyDataTable.DifficultyData diff = story.curDifficultyData;
            if (diff == null || string.IsNullOrEmpty(diff.difficultyName))
                return "normal";
            return diff.difficultyName.ToLowerInvariant();
        }

        private static float NegativeChance(string difficulty)
        {
            if (difficulty == "easy") return 0f;
            if (difficulty == "normal") return 0.25f;
            if (difficulty == "hard") return 0.5f;
            if (difficulty == "super") return 0.5f;
            return 0.25f;
        }

        internal static void OnMapLoaded(AbstractBattleSceneBase scene)
        {
            if (!Active || scene == null) return;
            RuntimeMapData map = MapDataField.GetValue(scene) as RuntimeMapData;
            if (map == null) return;
            string difficulty = CurrentDifficulty();
            float negativeChance = NegativeChance(difficulty);
            ItemController.ItemInfoTable infos =
                ItemInfosField.GetValue(scene) as ItemController.ItemInfoTable;
            if (infos != null &&
                Plugin.RandomPickupPool.Value != PickupPoolMode.Vanilla)
                ExpandPool(scene, infos, negativeChance);

            int cleared = 0;
            RuntimeMapData.BlockData[] blocks = map.blocks;
            if (blocks != null)
            {
                for (int i = 0; i < blocks.Length; i++)
                {
                    int t = (int)blocks[i].itemType;
                    if (t == 0) continue;
                    if (t == (int)ItemTypedef.TYPE.KEY ||
                        t == (int)ItemTypedef.TYPE.CRYSTAL)
                        continue;
                    blocks[i].itemType = (ItemTypedef.TYPE)0;
                    cleared++;
                }
            }

            int count = Plugin.RandomPickupCount.Value;
            if (count <= 0) count = (int)map.randomItemMax;
            if (count < 0) count = 0;
            if (count > 64) count = 64;
            RandomItemMinField.SetValue(map, (uint)count);
            RandomItemMaxField.SetValue(map, (uint)count);
            Plugin.Log.LogInfo("Randomizer: pickups " + count +
                " in random blocks" +
                (cleared > 0 ? ", replaced " + cleared + " fixed" : "") +
                ", pool " + Plugin.RandomPickupPool.Value +
                ", difficulty " + difficulty +
                ", negative chance " + negativeChance.ToString("P0"));
        }

        internal static void OnItemModelActive(Item.Model model)
        {
            if (!Active || !Plugin.HidePickupIcons.Value || model == null)
                return;
            GameObject go = ModelGoField.GetValue(model) as GameObject;
            if (go == null) return;
            InstallMark(go);
        }

        private static void InstallMark(GameObject modelGo)
        {
            Sprite sprite = BlankSprite;
            if (sprite == null) return;
            Transform existing = modelGo.transform.Find(MarkName);
            if (existing == null)
            {
                GameObject mark = new GameObject(MarkName);
                mark.transform.SetParent(modelGo.transform, false);
                mark.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                mark.transform.localRotation = Quaternion.identity;
                mark.transform.localScale = Vector3.one * 0.42f;
                SpriteRenderer sr = mark.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = Color.white;
                sr.sortingOrder = 20;
                mark.AddComponent<PickupBillboard>();
                existing = mark.transform;
            }
            else
            {
                existing.gameObject.SetActive(true);
            }
            Animator[] animators =
                modelGo.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                if (animators[i] != null) animators[i].enabled = false;
            }
            Renderer[] renderers =
                modelGo.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (r.transform == existing || r.transform.IsChildOf(existing))
                    continue;
                r.enabled = false;
            }
        }

        internal static void ShowToast(Player player, ItemTypedef.TYPE type,
            bool positive)
        {
            if (!Active || !Plugin.PickupToast.Value || player == null) return;
            if (type == ItemTypedef.TYPE.INVALID) return;
            if (player.playerIndex != 0) return;
            Font font = UiFont;
            if (font == null) return;
            GameObject go = new GameObject("BomberQoL_PickupToast");
            go.transform.position = player.pos + Vector3.up * 1.8f;
            TextMesh tm = go.AddComponent<TextMesh>();
            tm.text = ItemName(type);
            tm.font = font;
            tm.fontSize = 64;
            tm.characterSize = 0.35f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = positive
                ? new Color(1f, 0.92f, 0.35f, 1f)
                : new Color(1f, 0.35f, 0.35f, 1f);
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = font.material;
            go.AddComponent<PickupToast>();
        }
    }

    public class PickupBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    public class PickupToast : MonoBehaviour
    {
        private float _age;
        private TextMesh _text;

        private void Awake()
        {
            _text = GetComponent<TextMesh>();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (0.8f * Time.deltaTime);
            Camera cam = Camera.main;
            if (cam == null) cam = FindObjectOfType<Camera>();
            if (cam != null) transform.rotation = cam.transform.rotation;
            if (_text != null)
            {
                Color c = _text.color;
                c.a = Mathf.Clamp01(1f - Mathf.Max(0f, _age - 0.55f) / 0.75f);
                _text.color = c;
            }
            if (_age > 1.4f) Destroy(gameObject);
        }
    }

    [HarmonyPatch(typeof(StageSetupUI), "init")]
    internal static class RandomCharacterPatch
    {
        private static void Prefix()
        {
            Randomizer.RandomizeCharacters();
        }

        private static void Postfix(StageSetupUI __instance)
        {
            if (!Plugin.SkipCharacterSelect.Value) return;
            // Practice keeps the normal character-select screen.
            if (Practice.Active && !RandomRun.Active) return;
            if (!RandomRun.Active && !Plugin.RandomStoryCharacters.Value)
                return;
            __instance.StartCoroutine(AutoStart(__instance));
        }

        private static IEnumerator AutoStart(StageSetupUI ui)
        {
            yield return new WaitForSeconds(0.25f);
            if (ui != null) ui.moveNextScene(1);
        }
    }

    internal static class RandomRun
    {
        internal static bool Active;
        private static int _cleared;
        private static int _bossTotal;
        private static readonly HashSet<int> BossesDefeated =
            new HashSet<int>();

        private struct StageRef
        {
            internal int world;
            internal int stage;
            internal StageRef(int w, int s) { world = w; stage = s; }
        }

        private static MapDataManager.StoryMapID MapId(int world, int stage)
        {
            MapDataManager.StoryMapID id = new MapDataManager.StoryMapID();
            id.worldId = (byte)world;
            id.stageId = (byte)stage;
            return id;
        }

        private static bool IsBossStage(MapDataManager md, int world, int stage)
        {
            if (md == null) return stage >= 9;
            StoryMapInfoDataTable.StoryMapInfoData info =
                md.getStoryMapInfo(MapId(world, stage));
            return info != null && (int)info.appearBossType != 0;
        }

        private static List<StageRef> NormalCandidates()
        {
            MapDataManager md = SingletonBase<MapDataManager>.getInstance();
            var list = new List<StageRef>();
            for (int world = 1; world <= 7; world++)
            {
                int maxStage = world == 6 ? 2 : 10;
                for (int stage = 1; stage <= maxStage; stage++)
                {
                    if (IsBossStage(md, world, stage)) continue;
                    list.Add(new StageRef(world, stage));
                }
            }
            return list;
        }

        private static List<StageRef> BossCandidates()
        {
            MapDataManager md = SingletonBase<MapDataManager>.getInstance();
            var list = new List<StageRef>();
            for (int world = 1; world <= 7; world++)
            {
                int maxStage = world == 6 ? 2 : 10;
                for (int stage = 1; stage <= maxStage; stage++)
                {
                    if (IsBossStage(md, world, stage))
                        list.Add(new StageRef(world, stage));
                }
            }
            return list;
        }

        private static int BossKey(StageRef s) { return s.world * 100 + s.stage; }

        internal static void Begin()
        {
            Active = true;
            _cleared = 0;
            BossesDefeated.Clear();
            _bossTotal = BossCandidates().Count;
            Plugin.Log.LogInfo("Random run: 10 stages per boss, " +
                _bossTotal + " boss battles total");
        }

        internal static void Cancel()
        {
            Active = false;
            _cleared = 0;
            BossesDefeated.Clear();
        }

        internal static void OnSceneLoaded(Scene scene)
        {
            if (scene.name == "Title" || scene.name == "MainMenu")
                Cancel();
        }

        private static bool FindBattle(StoryManager story, int world,
            int stage, out int flowId)
        {
            flowId = -1;
            StoryFlowDataTable table = story.getStoryFlow(world);
            if (table == null) return false;
            for (int i = 0; i < table.storyFlowDataCount; i++)
            {
                StoryFlowDataTable.StoryFlowData f = table.getStoryFlowData(i);
                if (f == null || (int)f.eventType != 2) continue;
                if (f.stageId != stage) continue;
                flowId = f.storyFlowId;
                return true;
            }
            return false;
        }

        private static int FlowStage(StoryManager story, int world, int flowId)
        {
            StoryFlowDataTable table = story.getStoryFlow(world);
            if (table == null) return -1;
            StoryFlowDataTable.StoryFlowData f =
                table.getStoryFlowData(flowId);
            return f == null ? -1 : f.stageId;
        }

        internal static bool FirstNormal(out int world, out int stage)
        {
            List<StageRef> candidates = NormalCandidates();
            if (candidates.Count == 0)
            {
                world = 1;
                stage = 1;
                return false;
            }
            StageRef pick = candidates[Randomizer.Next(0, candidates.Count)];
            world = pick.world;
            stage = pick.stage;
            return true;
        }

        private static void ChooseNormal(StoryManager story)
        {
            List<StageRef> candidates = NormalCandidates();
            if (candidates.Count == 0)
            {
                Plugin.Log.LogWarning("Random run: no normal stages");
                return;
            }
            StageRef pick = candidates[Randomizer.Next(0, candidates.Count)];
            int flowId;
            if (!FindBattle(story, pick.world, pick.stage, out flowId))
            {
                Plugin.Log.LogWarning("Random run: missing battle");
                return;
            }
            story.setWorldId(pick.world, flowId - 1);
            Practice.CurrentWorld = pick.world;
            Practice.CurrentStage = pick.stage;
            Practice.CurrentFlowId = flowId;
            Plugin.Log.LogInfo("Random run: normal " + _cleared +
                "/10 -> world " + pick.world + " stage " + pick.stage);
        }

        private static void ChooseBoss(StoryManager story)
        {
            List<StageRef> candidates = BossCandidates();
            var open = new List<StageRef>();
            for (int i = 0; i < candidates.Count; i++)
            {
                StageRef b = candidates[i];
                if (BossesDefeated.Contains(BossKey(b))) continue;
                // World 7 stage 10 is the final story boss; keep it last.
                if (b.world == 7 && b.stage == 10 &&
                    BossesDefeated.Count < _bossTotal - 1) continue;
                open.Add(b);
            }
            if (open.Count == 0)
            {
                Active = false;
                Plugin.Log.LogInfo("Random run: all bosses defeated");
                return;
            }
            StageRef pick = open[Randomizer.Next(0, open.Count)];
            int flowId;
            if (!FindBattle(story, pick.world, pick.stage, out flowId))
            {
                Plugin.Log.LogWarning("Random run: boss missing");
                return;
            }
            story.setWorldId(pick.world, flowId - 1);
            Practice.CurrentWorld = pick.world;
            Practice.CurrentStage = pick.stage;
            Practice.CurrentFlowId = flowId;
            Plugin.Log.LogInfo("Random run: boss " +
                (BossesDefeated.Count + 1) + "/" + _bossTotal +
                " -> world " + pick.world + " stage " + pick.stage);
        }

        internal static void SelectNext(StoryManager story)
        {
            if (!Active) return;
            int world = story.worldId;
            int stage = FlowStage(story, world, story.storyFlowId);
            MapDataManager md = SingletonBase<MapDataManager>.getInstance();
            bool boss = IsBossStage(md, world, stage);

            if (boss)
            {
                StageRef s = new StageRef(world, stage);
                if (BossesDefeated.Add(BossKey(s)))
                    Plugin.Log.LogInfo("Random run: boss defeated " + world +
                        "-" + stage + " (" + BossesDefeated.Count + "/" +
                        _bossTotal + ")");
                if (BossesDefeated.Count >= _bossTotal)
                {
                    Active = false;
                    Plugin.Log.LogInfo("Random run: final boss defeated");
                    return;
                }
                _cleared = 0;
                ChooseNormal(story);
                return;
            }

            _cleared++;
            if (_cleared >= 10)
                ChooseBoss(story);
            else
                ChooseNormal(story);
        }
    }

    [HarmonyPatch(typeof(StoryUtility), "initToNext")]
    internal static class RandomRunPatch
    {
        private static void Prefix(GameSceneManager gsMgr, StoryManager strMgr,
            bool incStoryFlow)
        {
            if (!incStoryFlow) return;
            if (gsMgr == null || strMgr == null) return;
            string scene = gsMgr.mainSceneName;
            if (scene != "StoryBattle" &&
                gsMgr.prevSceneName != "StoryBattle")
            {
                if (RandomRun.Active || Practice.Active)
                    Plugin.Log.LogInfo("Practice: not advancing from scene '" +
                        scene + "' (prev '" + gsMgr.prevSceneName + "')");
                return;
            }
            if (Practice.Active && Practice.RepeatStage)
            {
                if (Practice.RepeatCurrentStage()) return;
            }
            if (!RandomRun.Active) return;
            Plugin.Log.LogInfo("Random run: advancing after battle (" +
                scene + ")");
            RandomRun.SelectNext(strMgr);
        }
    }


    [HarmonyPatch(typeof(AbstractBattleSceneBase), "endLoadMapData")]
    internal static class PickupMapPatch
    {
        private static void Postfix(AbstractBattleSceneBase __instance)
        {
            Pickups.OnMapLoaded(__instance);
        }
    }

    [HarmonyPatch(typeof(Item.Model), "setActive")]
    internal static class PickupIconPatch
    {
        private static void Postfix(Item.Model __instance, bool __0)
        {
            if (__0) Pickups.OnItemModelActive(__instance);
        }
    }

    [HarmonyPatch(typeof(Player), "addItem")]
    internal static class PickupToastPatch
    {
        private static void Postfix(Player __instance, ItemTypedef.TYPE __0,
            bool __1, bool __result)
        {
            if (__result) Pickups.ShowToast(__instance, __0, __1);
        }
    }

    [HarmonyPatch(typeof(StoryBattleUI), "openContinueDialog")]
    internal static class GemContinuePatch
    {
        private static readonly FieldInfo SceneField =
            AccessTools.Field(typeof(StoryBattleUI), "_scene");

        private static bool Prefix(StoryBattleUI __instance)
        {
            if (!Plugin.SkipGemContinue.Value) return true;
            StoryBattleScene scene =
                SceneField.GetValue(__instance) as StoryBattleScene;
            if (scene == null) return true;
            if (scene.interrupt("MainMenu", false))
            {
                Plugin.Log.LogInfo(
                    "Story: game over -> MainMenu (gem continue skipped)");
                return false;
            }
            return true;
        }
    }

    internal static class Practice
    {
        internal static bool Active;
        internal static bool RepeatStage;
        internal static int RunStatsMode = 4;
        internal static string RunDifficulty = "easy";
        internal static int CurrentWorld = 1;
        internal static int CurrentStage = 1;
        internal static int CurrentFlowId = 1;

        private static int _lastStatsKey = int.MinValue;
        private static bool _statsApplied;

        internal static void OnSceneLoaded(Scene scene)
        {
            if (scene.name == "Title" || scene.name == "MainMenu")
            {
                Active = false;
                RepeatStage = false;
            }
        }

        private static readonly int[] StatIndices = { 1, 4, 6, 8, 9, 10, 11, 12, 14 };
        private static readonly int[] MaxStats = { 5, 5, 4, 1, 1, 1, 1, 1, 1 };

        private static void ApplyStats(SaveDataManager.StoryData storyData,
            int mode, string difficulty)
        {
            if (storyData == null) return;
            if (difficulty == "super" && Plugin.MasterStatReset.Value)
            {
                storyData.resetStoryPlayParams();
                Plugin.Log.LogInfo("Practice stats reset for Master");
                return;
            }
            if (mode == 0) return;
            storyData.resetStoryPlayParams();
            if (mode == 1)
            {
                Plugin.Log.LogInfo("Practice stats mode: default");
                return;
            }

            int[] values;
            if (mode == 2)
            {
                values = MaxStats;
            }
            else if (mode == 4)
            {
                values = new int[StatIndices.Length];
                values[0] = Randomizer.Next(1, 5);
                values[1] = Randomizer.Next(1, 5);
                values[2] = Randomizer.Next(1, 4);
                for (int i = 3; i < values.Length; i++)
                    values[i] = Randomizer.Next(0, 2);
            }
            else
            {
                values = new[]
                {
                    Plugin.PracticeFire.Value,
                    Plugin.PracticeBomb.Value,
                    Plugin.PracticeSpeed.Value,
                    Plugin.PracticePunch.Value,
                    Plugin.PracticeGlove.Value,
                    Plugin.PracticeKick.Value,
                    Plugin.PracticeRemote.Value,
                    Plugin.PracticePenetration.Value,
                    Plugin.PracticeElasticity.Value
                };
            }

            for (int c = 0; c < storyData.characterDatas.Length; c++)
            {
                byte[] items = storyData.characterDatas[c].items;
                if (items == null) continue;
                for (int i = 0; i < StatIndices.Length; i++)
                {
                    int idx = StatIndices[i];
                    if (idx < 0 || idx >= items.Length) continue;
                    int v = values[i];
                    if (v < 0) v = 0;
                    if (v > 9) v = 9;
                    items[idx] = (byte)v;
                }
            }
            Plugin.Log.LogInfo("Practice stats mode: " +
                (mode == 2 ? "max" : mode == 4 ? "random" : "custom"));
        }

        internal static void ApplyRunStats(SaveDataManager.StoryData storyData)
        {
            if (storyData == null) return;
            if (!RandomRun.Active && !Active) return;
            bool perStage = Plugin.ResetStatsPerStage.Value ||
                !RandomRun.Active;
            int key = CurrentWorld * 1000 + CurrentStage;
            if (perStage && key != _lastStatsKey)
            {
                _lastStatsKey = key;
                _statsApplied = false;
            }
            if (_statsApplied) return;
            _statsApplied = true;
            ApplyStats(storyData, RunStatsMode, RunDifficulty);
        }

        internal static bool StartRandomRun(string difficulty, int statsMode)
        {
            RandomRun.Begin();
            RunDifficulty = difficulty;
            RunStatsMode = statsMode;
            RepeatStage = false;
            _lastStatsKey = int.MinValue;
            _statsApplied = false;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                int world;
                int stage;
                if (!RandomRun.FirstNormal(out world, out stage)) break;
                if (Start(world, stage, difficulty))
                    return true;
            }
            RandomRun.Cancel();
            return false;
        }

        internal static bool StartPractice(int world, int stage,
            string difficulty, bool repeat)
        {
            RandomRun.Cancel();
            RunDifficulty = difficulty;
            RunStatsMode = 1; // practice always starts from default stats
            RepeatStage = repeat;
            _lastStatsKey = int.MinValue;
            _statsApplied = false;
            return Start(world, stage, difficulty);
        }

        internal static bool RepeatCurrentStage()
        {
            if (!Active || !RepeatStage) return false;
            StoryManager story = SingletonBase<StoryManager>.getInstance();
            if (story == null) return false;
            story.setWorldId(CurrentWorld, CurrentFlowId - 1);
            _statsApplied = false;
            Plugin.Log.LogInfo("Practice: repeating world " + CurrentWorld +
                " stage " + CurrentStage);
            return true;
        }

        internal static bool Start(int world, int stage, string difficulty)
        {
            StoryManager story = SingletonBase<StoryManager>.getInstance();
            GameSceneManager gs =
                SingletonBaseWithGameObject<GameSceneManager>.getInstance();
            if (story == null || gs == null)
            {
                Plugin.Log.LogWarning("Practice: managers not ready");
                return false;
            }

            StoryFlowDataTable table = story.getStoryFlow(world);
            if (table == null)
            {
                Plugin.Log.LogWarning("Practice: no flow table for world " + world);
                return false;
            }

            StoryFlowDataTable.StoryFlowData target = null;
            for (int i = 0; i < table.storyFlowDataCount; i++)
            {
                StoryFlowDataTable.StoryFlowData f = table.getStoryFlowData(i);
                if (f == null) continue;
                if ((int)f.eventType != 2) continue;
                if (f.stageId != stage) continue;
                target = f;
                break;
            }
            if (target == null)
            {
                Plugin.Log.LogWarning("Practice: no battle flow for world " +
                    world + " stage " + stage);
                return false;
            }

            DifficultyDataTable.DifficultyData diff =
                story.findDifficulty(difficulty);
            if (diff != null) story.setCurrentDifficulty(diff);
            story.initPlayerStockNum((uint)world);
            story.setWorldId(world, target.storyFlowId - 1);

            MoveSceneParam.StoryParam sp = gs.param.storyParam;
            sp.set(target);
            gs.param.storyParam = sp;

            SaveDataManager sd =
                SingletonBaseWithGameObject<SaveDataManager>.getInstance();
            if (sd != null && sd.storyData != null)
            {
                sd.storyData.stock = story.playerStockNum;
                if (diff != null) sd.storyData.difficulty = (int)diff.id;
                sd.storyData.setStoryFlowId(world, target.storyFlowId);
            }

            CurrentWorld = world;
            CurrentStage = stage;
            CurrentFlowId = target.storyFlowId;
            RunDifficulty = difficulty;
            Active = true;
            Plugin.Log.LogInfo("Practice: world " + world + " stage " + stage +
                " (" + difficulty + "), story saves suppressed" +
                (RepeatStage ? ", repeat on" : ""));
            gs.moveScene("StageSetup");
            return true;
        }
    }

    [HarmonyPatch(typeof(StoryManager), "setFromSaveDataToBattleParams")]
    internal static class PracticeStatsPatch
    {
        private static void Prefix(SaveDataManager.StoryData __0)
        {
            Practice.ApplyRunStats(__0);
        }
    }

    public class PracticeMenu : MonoBehaviour
    {
        internal static bool Visible;
        internal static PracticeMenu Instance;

        private static readonly string[] Difficulties =
            { "easy", "normal", "hard", "super" };
        private static readonly string[] DiffLabels =
            { "Beginner", "Veteran", "Expert", "Master" };
        private static readonly string[] StatLabels =
            { "Save", "Default", "Max", "Custom", "Random" };

        private bool _visible;
        private bool _prevCursorVisible;
        private CursorLockMode _prevLockMode;
        private int _tab;
        private int _difficulty;
        private int _stats = 4;
        private int _world = 1;
        private int _stage = 1;
        private int _row;
        private bool _leftHeld;
        private bool _rightHeld;
        private bool _upHeld;
        private bool _downHeld;
        private float _comboHeld;
        private float _lastPadLog;
        private bool _comboLatched;
        private Vector2 _helpScroll;

        private void Awake()
        {
            Instance = this;
        }

        internal static void OpenFromMenu()
        {
            if (Instance != null) Instance.SetVisible(true);
        }

        internal static void CloseFromMenu()
        {
            if (Instance != null) Instance.SetVisible(false);
        }

        private static int MaxStage(int world)
        {
            return world == 6 ? 2 : 10;
        }

        private int RowCount
        {
            get
            {
                if (_tab == 0) return 5;
                if (_tab == 1) return 13;
                if (_tab == 2) return 4;
                if (_tab == 3) return 15;
                return 0;
            }
        }

        private void ClampStage()
        {
            int max = MaxStage(_world);
            if (_stage > max) _stage = max;
            if (_stage < 1) _stage = 1;
        }

        private void Update()
        {
            if (Plugin.PracticeMenuKey != KeyCode.None &&
                Input.GetKeyDown(Plugin.PracticeMenuKey))
            {
                SetVisible(!_visible);
                return;
            }

            if (Plugin.PracticeMenuDebug.Value &&
                Plugin.PracticeDebugHeld() &&
                Time.unscaledTime - _lastPadLog > 1f)
            {
                _lastPadLog = Time.unscaledTime;
                Plugin.LogPracticePadState();
            }
            if (Plugin.PracticeComboHeld())
            {
                if (!_comboLatched)
                {
                    float hold = Plugin.PracticeMenuPadHold.Value;
                    if (hold < 0.2f) hold = 0.2f;
                    if (hold > 3f) hold = 3f;
                    _comboHeld += Time.unscaledDeltaTime;
                    if (_comboHeld >= hold)
                    {
                        _comboHeld = 0f;
                        _comboLatched = true;
                        SetVisible(!_visible);
                    }
                }
            }
            else
            {
                _comboHeld = 0f;
                _comboLatched = false;
            }

            if (!_visible) return;

            if (Input.GetKeyDown(KeyCode.Q) || PadButtonDown("L"))
            {
                _tab = (_tab + 4) % 5;
                _row = 0;
            }
            if (Input.GetKeyDown(KeyCode.E) || PadButtonDown("R"))
            {
                _tab = (_tab + 1) % 5;
                _row = 0;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _tab = (_tab + 1) % 5;
                _row = 0;
            }
            HandleNavigation();
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private static bool RawPadButtonDown(int button)
        {
            if (button < 0 || button > 19) return false;
            if (Input.GetKeyDown((KeyCode)(330 + button))) return true;
            for (int joy = 1; joy <= 8; joy++)
            {
                if (Input.GetKeyDown(
                    (KeyCode)(350 + (joy - 1) * 20 + button)))
                    return true;
            }
            return false;
        }

        private static bool PadButtonDown(string name)
        {
            int raw = -1;
            if (name == "A") raw = 0;
            else if (name == "B") raw = 1;
            else if (name == "L") raw = 4;
            else if (name == "R") raw = 5;
            if (raw >= 0 && RawPadButtonDown(raw)) return true;

            InputAdapter adapter =
                SingletonBaseWithGameObject<InputAdapter>.getInstance();
            if (adapter == null) return false;
            try { return adapter.GetButtonDown(name + "_1P"); }
            catch { return false; }
        }

        private static float PadAxis(string name)
        {
            InputAdapter adapter =
                SingletonBaseWithGameObject<InputAdapter>.getInstance();
            if (adapter != null)
            {
                try
                {
                    float value = adapter.GetAxis(name + "_1P");
                    if (Mathf.Abs(value) > 0.01f) return value;
                }
                catch { }
            }
            if (name == "HORIZONTAL")
                return Input.GetAxisRaw("Horizontal");
            if (name == "VERTICAL")
                return Input.GetAxisRaw("Vertical");
            return 0f;
        }

        private static void Toggle(ConfigEntry<bool> entry)
        {
            entry.Value = !entry.Value;
            if (entry == Plugin.PlatformExclusives && entry.Value)
                PlatformCharacters.ApplyLocaleNames();
            if (entry == Plugin.FastResults)
                Plugin.ApplyResultTweaks();
            if (entry == Plugin.UnlockMaster)
                Plugin.EnableMasterButton();
            if (entry == Plugin.MainMenuQoL)
                MainMenuQoLUtility.Apply();
            MainMenuQoLUtility.Apply();
        }

        private static void ToggleInverted(ConfigEntry<bool> entry)
        {
            entry.Value = !entry.Value;
            Plugin.ApplyAllPostFx();
        }

        private static void ToggleAutoSkipMovies()
        {
            Plugin.AutoSkipMovies.Value =
                Plugin.AutoSkipMovies.Value > 0f ? 0f : 5f;
        }

        private static void ToggleDifficultyOdds()
        {
            Plugin.RandomPickupPool.Value =
                Plugin.RandomPickupPool.Value == PickupPoolMode.Difficulty
                    ? PickupPoolMode.Expanded
                    : PickupPoolMode.Difficulty;
        }

        private void ChangeRowValue(int delta)
        {
            if (_tab == 4) return;
            if (_tab == 0)
            {
                switch (_row)
                {
                    case 0:
                        _difficulty = Mathf.Clamp(_difficulty + delta, 0,
                            DiffLabels.Length - 1);
                        break;
                    case 1:
                        _world = Mathf.Clamp(_world + delta, 1, 7);
                        ClampStage();
                        break;
                    case 2:
                        _stage = Mathf.Clamp(_stage + delta, 1,
                            MaxStage(_world));
                        break;
                    case 3:
                        Toggle(Plugin.PracticeRepeatStage);
                        break;
                }
                return;
            }

            if (_tab == 2)
            {
                switch (_row)
                {
                    case 0: Toggle(Plugin.SmarterBots); break;
                    case 1: Toggle(Plugin.RandomCpuOpponent); break;
                    case 2: Toggle(Plugin.CpuSpecialAbilities); break;
                    case 3: Toggle(Plugin.PlatformExclusives); break;
                }
                return;
            }

            if (_tab == 3)
            {
                switch (_row)
                {
                    case 0: ToggleInverted(Plugin.NoMotionBlur); break;
                    case 1: ToggleInverted(Plugin.NoDepthOfField); break;
                    case 2: ToggleInverted(Plugin.NoBloom); break;
                    case 3: ToggleInverted(Plugin.NoVignette); break;
                    case 4: ToggleInverted(Plugin.NoGrain); break;
                    case 5: Toggle(Plugin.FastResults); break;
                    case 6: Toggle(Plugin.UnlockMaster); break;
                    case 7: Toggle(Plugin.SkipEnabled); break;
                    case 8: Toggle(Plugin.AutoSkipLogos); break;
                    case 9: ToggleAutoSkipMovies(); break;
                    case 10: Toggle(Plugin.SoftResetEnabled); break;
                    case 11: Toggle(Plugin.CustomBgmShuffle); break;
                    case 12: Toggle(Plugin.CustomSfxShuffle); break;
                    case 13: Toggle(Plugin.MainMenuQoL); break;
                }
                return;
            }

            switch (_row)
            {
                case 0:
                    _difficulty = Mathf.Clamp(_difficulty + delta, 0,
                        DiffLabels.Length - 1);
                    break;
                case 1:
                    _stats = Mathf.Clamp(_stats + delta, 0,
                        StatLabels.Length - 1);
                    break;
                case 2: Toggle(Plugin.RandomPickups); break;
                case 3: Toggle(Plugin.HidePickupIcons); break;
                case 4: Toggle(Plugin.PickupToast); break;
                case 5: ToggleDifficultyOdds(); break;
                case 6: Toggle(Plugin.RandomStoryCharacters); break;
                case 7: Toggle(Plugin.RandomEnemies); break;
                case 8: Toggle(Plugin.SkipCharacterSelect); break;
                case 9: Toggle(Plugin.ResetStatsPerStage); break;
                case 10: Toggle(Plugin.MasterStatReset); break;
                case 11: Toggle(Plugin.SkipGemContinue); break;
            }
        }

        private void ActivateRow()
        {
            if (_tab == 4) return;
            if (_tab == 0 && _row == 4)
            {
                StartPractice();
                return;
            }
            if (_tab == 1 && _row == 12)
            {
                StartRun();
                return;
            }
            if (_tab == 3 && _row == 14)
            {
                MainMenuOptionPatch.OpenOriginalOptions();
                return;
            }
            ChangeRowValue(1);
        }

        private void HandleNavigation()
        {
            float dx = PadAxis("CROSS_HORIZONTAL");
            float dy = PadAxis("CROSS_VERTICAL");
            float sx = PadAxis("HORIZONTAL");
            float sy = PadAxis("VERTICAL");
            if (Mathf.Abs(sx) < 0.35f) sx = 0f;
            if (Mathf.Abs(sy) < 0.35f) sy = 0f;
            float ax = Mathf.Abs(sx) > Mathf.Abs(dx) ? sx : dx;
            float ay = Mathf.Abs(sy) > Mathf.Abs(dy) ? sy : dy;

            bool left = Input.GetKeyDown(KeyCode.LeftArrow) ||
                (ax < -0.5f && !_leftHeld);
            bool right = Input.GetKeyDown(KeyCode.RightArrow) ||
                (ax > 0.5f && !_rightHeld);
            bool up = Input.GetKeyDown(KeyCode.UpArrow) ||
                (ay > 0.5f && !_upHeld);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) ||
                (ay < -0.5f && !_downHeld);
            _leftHeld = ax < -0.5f;
            _rightHeld = ax > 0.5f;
            _upHeld = ay > 0.5f;
            _downHeld = ay < -0.5f;

            if (_tab == 4)
            {
                if (up) _helpScroll.y = Mathf.Max(0f,
                    _helpScroll.y - 40f);
                if (down) _helpScroll.y += 40f;
            }
            else
            {
                if (left || right) ChangeRowValue(left ? -1 : 1);
                if (up && RowCount > 0)
                    _row = (_row + RowCount - 1) % RowCount;
                if (down && RowCount > 0)
                    _row = (_row + 1) % RowCount;
            }

            bool accept = Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                PadButtonDown("A");
            bool cancel = Input.GetKeyDown(KeyCode.Escape) ||
                PadButtonDown("B");
            if (cancel) { SetVisible(false); return; }
            if (accept) ActivateRow();
        }

        private void StartPractice()
        {
            if (Practice.StartPractice(_world, _stage,
                Difficulties[_difficulty],
                Plugin.PracticeRepeatStage.Value))
                SetVisible(false);
        }

        private void StartRun()
        {
            if (Practice.StartRandomRun(
                Difficulties[_difficulty], _stats))
                SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            Visible = visible;
            _row = 0;
            Plugin.Log.LogInfo("Practice menu " +
                (visible ? "opened" : "closed"));
            if (visible)
            {
                _prevCursorVisible = Cursor.visible;
                _prevLockMode = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                Cursor.visible = _prevCursorVisible;
                Cursor.lockState = _prevLockMode;
            }
        }

        private GUIStyle _rowSelectedStyle;
        private GUIStyle _rowNormalStyle;
        private Texture2D _rowSelectedTex;
        private Texture2D _rowNormalTex;

        private static readonly Color MarkerColor =
            new Color(1f, 0.84f, 0.25f, 1f);

        private static Texture2D MakeSolid(Color color)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        private GUIStyle RowStyle(int row)
        {
            if (_rowSelectedStyle == null)
            {
                _rowSelectedTex = MakeSolid(
                    new Color(0.99f, 0.78f, 0.18f, 0.32f));
                _rowNormalTex = MakeSolid(new Color(0f, 0f, 0f, 0f));
                _rowSelectedStyle = new GUIStyle();
                _rowSelectedStyle.normal.background = _rowSelectedTex;
                _rowSelectedStyle.padding = new RectOffset(6, 6, 3, 3);
                _rowSelectedStyle.margin = new RectOffset(0, 0, 1, 1);
                _rowNormalStyle = new GUIStyle();
                _rowNormalStyle.normal.background = _rowNormalTex;
                _rowNormalStyle.padding = new RectOffset(6, 6, 3, 3);
                _rowNormalStyle.margin = new RectOffset(0, 0, 1, 1);
            }
            return row == _row ? _rowSelectedStyle : _rowNormalStyle;
        }

        private void BeginRow(int row)
        {
            GUILayout.BeginHorizontal(RowStyle(row));
        }

        private void EndRow()
        {
            GUILayout.EndHorizontal();
        }

        private bool RowSelected(int row)
        {
            return row == _row;
        }

        private void ValueButton(int row, int value, int current,
            string text, float width, Action action)
        {
            Color bg = GUI.backgroundColor;
            Color fg = GUI.contentColor;
            if (value == current)
            {
                GUI.backgroundColor = MarkerColor;
                GUI.contentColor = Color.black;
            }
            else if (RowSelected(row))
            {
                GUI.contentColor = MarkerColor;
            }
            if (GUILayout.Button(text, GUILayout.Width(width),
                GUILayout.Height(26)))
                action();
            GUI.backgroundColor = bg;
            GUI.contentColor = fg;
        }

        private void DrawDifficulty()
        {
            GUILayout.BeginVertical(RowStyle(0));
            GUILayout.Label((RowSelected(0) ? ">> " : "   ") +
                "Difficulty");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < DiffLabels.Length; i++)
            {
                int idx = i;
                ValueButton(0, idx, _difficulty, DiffLabels[idx], 100f,
                    delegate { _difficulty = idx; });
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawStats()
        {
            GUILayout.BeginVertical(RowStyle(1));
            GUILayout.Label((RowSelected(1) ? ">> " : "   ") + "Stats");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < StatLabels.Length; i++)
            {
                int idx = i;
                ValueButton(1, idx, _stats, StatLabels[idx], 90f,
                    delegate { _stats = idx; });
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawToggle(int row, ConfigEntry<bool> entry, string label)
        {
            GUILayout.BeginHorizontal(RowStyle(row));
            Color old = GUI.contentColor;
            if (RowSelected(row)) GUI.contentColor = MarkerColor;
            bool value = GUILayout.Toggle(entry.Value,
                (RowSelected(row) ? ">> " : "   ") + label);
            GUI.contentColor = old;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (value == entry.Value) return;
            entry.Value = value;
        }

        private void DrawDifficultyOddsToggle(int row)
        {
            GUILayout.BeginHorizontal(RowStyle(row));
            Color old = GUI.contentColor;
            if (RowSelected(row)) GUI.contentColor = MarkerColor;
            bool on = Plugin.RandomPickupPool.Value == PickupPoolMode.Difficulty;
            bool value = GUILayout.Toggle(on,
                (RowSelected(row) ? ">> " : "   ") +
                "Difficulty-based odds");
            GUI.contentColor = old;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (value == on) return;
            Plugin.RandomPickupPool.Value = value
                ? PickupPoolMode.Difficulty
                : PickupPoolMode.Expanded;
        }

        private void DrawPractice()
        {
            DrawDifficulty();

            GUILayout.BeginVertical(RowStyle(1));
            GUILayout.Label((RowSelected(1) ? ">> " : "   ") + "World");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 7; i++)
            {
                int world = i;
                ValueButton(1, world, _world, world.ToString(), 56f,
                    delegate { _world = world; ClampStage(); });
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(RowStyle(2));
            GUILayout.Label((RowSelected(2) ? ">> " : "   ") + "Stage");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= MaxStage(_world); i++)
            {
                int stage = i;
                ValueButton(2, stage, _stage, stage.ToString(), 48f,
                    delegate { _stage = stage; });
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal(RowStyle(3));
            Color oldColor = GUI.contentColor;
            if (RowSelected(3)) GUI.contentColor = MarkerColor;
            bool repeat = GUILayout.Toggle(Plugin.PracticeRepeatStage.Value,
                (RowSelected(3) ? ">> " : "   ") +
                "Repeat this stage when cleared");
            GUI.contentColor = oldColor;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (repeat != Plugin.PracticeRepeatStage.Value)
                Plugin.PracticeRepeatStage.Value = repeat;

            GUILayout.Space(10);
            GUILayout.BeginVertical(RowStyle(4));
            if (GUILayout.Button((RowSelected(4) ? ">> " : "   ") +
                "Start Practice", GUILayout.Height(36)))
                StartPractice();
            GUILayout.EndVertical();
            GUILayout.Label(RowSelected(4)
                ? ">> A: start    B: close    L/R: tabs"
                : "   A: start    B: close    L/R: tabs");
        }

        private void DrawRandomizer()
        {
            DrawDifficulty();
            DrawStats();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawToggle(2, Plugin.RandomPickups, "Random pickups");
            DrawToggle(3, Plugin.HidePickupIcons, "Blank pickup icons");
            DrawToggle(4, Plugin.PickupToast, "Pickup name popup");
            DrawDifficultyOddsToggle(5);
            DrawToggle(6, Plugin.RandomStoryCharacters, "Random characters");
            DrawToggle(7, Plugin.RandomEnemies, "Random enemies");
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawToggle(8, Plugin.SkipCharacterSelect, "Skip character select");
            DrawToggle(9, Plugin.ResetStatsPerStage, "Reset stats per stage");
            DrawToggle(10, Plugin.MasterStatReset, "Master stat reset");
            DrawToggle(11, Plugin.SkipGemContinue, "Skip gem continue");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("10 random stages + undefeated bosses.  " +
                "Finish all bosses; world 7 last.");
            GUILayout.Label("Beginner all positive.  Veteran 25%.  " +
                "Expert: 50/50.  Master: 50/50 + reset.");
            GUILayout.BeginVertical(RowStyle(12));
            if (GUILayout.Button((RowSelected(12) ? ">> " : "   ") +
                "Start Random Run", GUILayout.Height(36)))
                StartRun();
            GUILayout.EndVertical();
            GUILayout.Label(RowSelected(12)
                ? ">> A: start    B: close    L/R: tabs"
                : "   A: start    B: close    L/R: tabs");
        }

        private void DrawMultiplayer()
        {
            GUILayout.Label("Local battle only.");
            GUILayout.Space(6);
            DrawToggle(0, Plugin.SmarterBots, "Smarter bots");
            DrawToggle(1, Plugin.RandomCpuOpponent, "Random CPU opponent");
            DrawToggle(2, Plugin.CpuSpecialAbilities,
                "CPU A-button special abilities");
            DrawToggle(3, Plugin.PlatformExclusives,
                "Platform exclusives");
            GUILayout.Space(8);
            GUILayout.Label("A: toggle    B: close    L/R: tabs");
        }

        private void DrawInvertedToggle(int row, ConfigEntry<bool> entry,
            string label)
        {
            GUILayout.BeginHorizontal(RowStyle(row));
            Color old = GUI.contentColor;
            if (RowSelected(row)) GUI.contentColor = MarkerColor;
            bool on = !entry.Value;
            bool value = GUILayout.Toggle(on,
                (RowSelected(row) ? ">> " : "   ") + label);
            GUI.contentColor = old;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (value == on) return;
            entry.Value = !value;
            Plugin.ApplyAllPostFx();
        }

        private void DrawAutoSkipToggle(int row)
        {
            GUILayout.BeginHorizontal(RowStyle(row));
            Color old = GUI.contentColor;
            if (RowSelected(row)) GUI.contentColor = MarkerColor;
            bool on = Plugin.AutoSkipMovies.Value > 0f;
            bool value = GUILayout.Toggle(on,
                (RowSelected(row) ? ">> " : "   ") +
                "Auto-skip movies (5s)");
            GUI.contentColor = old;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            if (value == on) return;
            Plugin.AutoSkipMovies.Value = value ? 5f : 0f;
        }

        private void DrawSettings()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawInvertedToggle(0, Plugin.NoMotionBlur, "Motion blur");
            DrawInvertedToggle(1, Plugin.NoDepthOfField, "Depth of field");
            DrawInvertedToggle(2, Plugin.NoBloom, "Bloom");
            DrawInvertedToggle(3, Plugin.NoVignette, "Vignette");
            DrawInvertedToggle(4, Plugin.NoGrain, "Film grain");
            DrawToggle(5, Plugin.FastResults, "Fast results");
            DrawToggle(6, Plugin.UnlockMaster, "Master difficulty");
            GUILayout.EndVertical();
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawToggle(7, Plugin.SkipEnabled, "Skip movies");
            DrawToggle(8, Plugin.AutoSkipLogos, "Auto-skip logos");
            DrawAutoSkipToggle(9);
            DrawToggle(10, Plugin.SoftResetEnabled, "Soft reset");
            DrawToggle(11, Plugin.CustomBgmShuffle, "Shuffle BGM");
            DrawToggle(12, Plugin.CustomSfxShuffle, "Shuffle SFX");
            DrawToggle(13, Plugin.MainMenuQoL,
                "Bomber QoL main-menu entry");
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            string bgm = Plugin.CustomBgmFolder.Value;
            string bgmSource = Plugin.CustomBgmSource.Value;
            string sfx = Plugin.CustomSfxFolder.Value;
            GUILayout.Label("BGM source: " + (string.IsNullOrEmpty(bgmSource)
                ? (string.IsNullOrEmpty(bgm)
                    ? "none"
                    : Path.GetFileName(bgm.TrimEnd('/', '\\')))
                : Path.GetFileName(bgmSource.TrimEnd('/', '\\'))));
            GUILayout.Label("BGM cache: " + (string.IsNullOrEmpty(bgm)
                ? "none" : bgm));
            GUILayout.Label("SFX folder: " + (string.IsNullOrEmpty(sfx)
                ? "none"
                : Path.GetFileName(sfx.TrimEnd('/', '\\'))));
            GUILayout.Space(8);
            GUILayout.BeginVertical(RowStyle(14));
            if (GUILayout.Button((RowSelected(14) ? ">> " : "   ") +
                "Open original game options", GUILayout.Height(30)))
                MainMenuOptionPatch.OpenOriginalOptions();
            GUILayout.EndVertical();
            GUILayout.Label(RowSelected(14)
                ? ">> A: open    B: close    L/R: tabs"
                : "   A: toggle    B: close    L/R: tabs");
        }

        private GUIStyle _helpStyle;
        private GUIStyle _helpTitle;
        private GUIStyle _helpHeading;

        private void DrawHelp()
        {
            if (_helpStyle == null)
            {
                _helpStyle = new GUIStyle(GUI.skin.label);
                _helpStyle.wordWrap = true;
                _helpTitle = new GUIStyle(_helpStyle);
                _helpTitle.fontStyle = FontStyle.Bold;
                _helpTitle.fontSize = 14;
                _helpTitle.normal.textColor = MarkerColor;
                _helpHeading = new GUIStyle(_helpStyle);
                _helpHeading.fontStyle = FontStyle.Bold;
                _helpHeading.normal.textColor = MarkerColor;
            }
            _helpScroll = GUILayout.BeginScrollView(_helpScroll, false, true);

            GUILayout.Label("Bomber QoL", _helpTitle);
            GUILayout.Label(
                "D-pad / left stick: move    Left / Right: change value\n" +
                "A: toggle or open    B: close    L/R, Q/E or Tab: switch tabs\n" +
                "Up / Down on this page scrolls the text.", _helpStyle);

            GUILayout.Space(8);
            GUILayout.Label("Practice", _helpHeading);
            GUILayout.Label(
                "Pick a difficulty, world and stage and start one stage with " +
                "default stats. Story saves are suppressed, so practice does " +
                "not advance the real story. Optional repeat restarts the same " +
                "stage when cleared.", _helpStyle);

            GUILayout.Space(8);
            GUILayout.Label("Randomizer", _helpHeading);
            GUILayout.Label(
                "Random characters rerolls the character select at every stage " +
                "setup. Random enemies shuffles the enemy types that spawn on " +
                "each story/practice map using that map's own enemy palette. " +
                "Pickup options change what drops and how it is shown.", _helpStyle);
            GUILayout.Label(
                "Start Random Run builds a run of 10 random non-boss stages, " +
                "then a random undefeated boss. Every boss defeat starts " +
                "another 10-stage block. All 13 boss battles must be beaten; " +
                "world 7's final boss is always last.", _helpStyle);
            GUILayout.Label(
                "Pickup odds by difficulty: Beginner all positive, Veteran " +
                "25%, Expert 50/50, Master 50/50 with a per-stage stat reset.",
                _helpStyle);

            GUILayout.Space(8);
            GUILayout.Label("Multiplayer", _helpHeading);
            GUILayout.Label(
                "Local battle only. Smarter bots search farther and retarget " +
                "faster. Random CPU randomises COM character slots each " +
                "battle. CPU specials let CPUs use their A-button ability. " +
                "Platform exclusives unlock Master Chief, Ratchet and the " +
                "other built-in platform IDs; they are player-selectable but " +
                "cannot be CPU opponents because the Steam build has no NPC " +
                "AI data for them.", _helpStyle);

            GUILayout.Space(8);
            GUILayout.Label("Settings", _helpHeading);
            GUILayout.Label(
                "Runtime launcher options: post-processing effects, fast " +
                "results, Master difficulty, movie skipping, soft reset, " +
                "BGM/SFX shuffle and the Bomber QoL main-menu entry. The " +
                "original game options are still available from this tab.",
                _helpStyle);
            GUILayout.Label(
                "BGM folders are converted to an OGG cache by the launcher. " +
                "Folder pickers, launch options, voice selection, BepInEx " +
                "install and save backups stay in the launcher.", _helpStyle);

            GUILayout.Space(8);
            GUILayout.Label("Notes", _helpHeading);
            GUILayout.Label(
                "B is always the normal bomb; A is the character special. " +
                "Achievements are earned normally; the only helper is the " +
                "dead online-room unlock in the launcher.", _helpStyle);

            GUILayout.EndScrollView();
        }

        private void OnGUI()
        {
            if (!_visible) return;
            GUI.depth = -1000;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            int w = Mathf.Min(780, Screen.width - 20);
            int h = Mathf.Min(600, Screen.height - 20);
            Rect r = new Rect((Screen.width - w) / 2,
                (Screen.height - h) / 2, w, h);
            Color old = GUI.color;
            GUI.color = new Color(0.045f, 0.065f, 0.105f, 1f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.62f, 0.78f, 1f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1, r.width, 1),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height),
                Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1, r.y, 1, r.height),
                Texture2D.whiteTexture);
            GUI.color = old;

            GUIStyle title = new GUIStyle(GUI.skin.label);
            title.alignment = TextAnchor.MiddleCenter;
            title.fontStyle = FontStyle.Bold;
            title.normal.textColor = new Color(1f, 0.88f, 0.55f);
            GUI.Label(new Rect(r.x, r.y + 4, r.width, 20),
                "Bomber QoL — Practice / Random Run", title);

            Color oldLabel = GUI.skin.label.normal.textColor;
            Color oldToggle = GUI.skin.toggle.normal.textColor;
            GUI.skin.label.normal.textColor = Color.white;
            GUI.skin.toggle.normal.textColor = Color.white;
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 28,
                w - 32, h - 44));

            GUILayout.BeginHorizontal();
            Color oldTabBg = GUI.backgroundColor;
            Color oldTabFg = GUI.contentColor;
            string[] tabNames = { "Practice", "Randomizer",
                "Multiplayer", "Settings", "Help" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tab = i;
                if (_tab == tab)
                {
                    GUI.backgroundColor = MarkerColor;
                    GUI.contentColor = Color.black;
                }
                string name = _tab == tab
                    ? ">> " + tabNames[tab] + " <<"
                    : tabNames[tab];
                if (GUILayout.Button(name, GUILayout.Height(30)))
                {
                    _tab = tab;
                    _row = 0;
                }
                GUI.backgroundColor = oldTabBg;
                GUI.contentColor = oldTabFg;
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label("L1/R1 or Q/E: tab");
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            if (_tab == 0)
                DrawPractice();
            else if (_tab == 1)
                DrawRandomizer();
            else if (_tab == 2)
                DrawMultiplayer();
            else if (_tab == 3)
                DrawSettings();
            else
                DrawHelp();

            GUILayout.EndArea();
            GUI.skin.label.normal.textColor = oldLabel;
            GUI.skin.toggle.normal.textColor = oldToggle;
        }
    }

    [HarmonyPatch(typeof(AbstractBattleSceneBase), "endLoadMapData")]
    internal static class RandomEnemiesPatch
    {
        private static readonly System.Random Rng = new System.Random();
        private static readonly FieldInfo MapField =
            AccessTools.Field(typeof(AbstractBattleSceneBase), "_mapData");

        private static void Postfix(AbstractBattleSceneBase __instance)
        {
            if (!Plugin.RandomEnemies.Value) return;
            if (__instance == null) return;
            if (SceneManager.GetActiveScene().name != "StoryBattle")
                return;
            RuntimeMapData map = MapField.GetValue(__instance) as RuntimeMapData;
            if (map == null) return;
            string[] pool = map.useEnemyList;
            RuntimeMapData.EnemyStartPointData[] points =
                map.enemyStartPointDatas;
            if (pool == null || pool.Length == 0 ||
                points == null || points.Length == 0)
                return;
            for (int i = 0; i < points.Length; i++)
                points[i].name = pool[Rng.Next(pool.Length)];
            Plugin.Log.LogInfo("Random enemies: " + points.Length +
                " spawns from " + pool.Length + " map types");
        }
    }

    [HarmonyPatch(typeof(NPCAIData), "getParam")]
    internal static class SmarterBotsPatch
    {
        private static bool _logged;

        private static void Postfix(ref NPCAIData.Param __result)
        {
            if (!Plugin.SmarterBots.Value) return;
            if (SceneManager.GetActiveScene().name != "LocalBattle")
                return;
            __result.searchRange = Mathf.Max(__result.searchRange, 10);
            NPCAIData.TacticalTargetParam target =
                __result.tacticalTargetParam;
            if (target.rethinkInterval <= 0f ||
                target.rethinkInterval > 0.25f)
                target.rethinkInterval = 0.25f;
            __result.tacticalTargetParam = target;
            if (!_logged)
            {
                _logged = true;
                Plugin.Log.LogInfo(
                    "Smarter Bots: search range >= 10, rethink <= 0.25s");
            }
        }
    }

    [HarmonyPatch(typeof(OfflineReadyScene), "execPreMoveScene")]
    internal static class RandomCpuOpponentPatch
    {
        private static readonly FieldInfo AiTypesField =
            AccessTools.Field(typeof(LocalBattleScene),
                "CHARACTER_NPC_AI_TYPES");
        private static int _lastReadyId;

        internal static void ResetGuard()
        {
            _lastReadyId = 0;
        }

        private static void Prefix(OfflineReadyScene __instance)
        {
            if (!Plugin.RandomCpuOpponent.Value) return;
            if (__instance == null) return;
            int readyId = __instance.GetInstanceID();
            if (readyId == _lastReadyId) return;
            _lastReadyId = readyId;
            GameSceneManager gs =
                SingletonBaseWithGameObject<GameSceneManager>.getInstance();
            CharacterDataManager cd =
                SingletonBase<CharacterDataManager>.getInstance();
            if (gs == null || cd == null) return;
            MoveSceneParam.CharacterParam[] chars = gs.param.characterParams;
            if (chars == null)
            {
                Plugin.Log.LogWarning(
                    "Multiplayer: random CPU skipped (no characterParams)");
                return;
            }

            int total = cd.getTotalCharacterCount();
            if (total <= 0) return;

            int npcType = (int)gs.param.localBattleRuleParam.npcType;
            CharacterDataManager.NPC_AI_TYPE aiType =
                CharacterDataManager.NPC_AI_TYPE.NORMAL;
            CharacterDataManager.NPC_AI_TYPE[] aiTypes = AiTypesField == null
                ? null
                : AiTypesField.GetValue(null) as
                    CharacterDataManager.NPC_AI_TYPE[];
            if (aiTypes != null && npcType >= 0 && npcType < aiTypes.Length)
                aiType = aiTypes[npcType];

            int comCount = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i].controlType == BattleDef.CONTROL_TYPE.COM)
                    comCount++;
            }
            Plugin.Log.LogInfo("Multiplayer: random CPU (ready) params=" +
                chars.Length + " com=" + comCount + " npcType=" + npcType +
                " aiType=" + aiType);
            Plugin.Log.LogInfo("Multiplayer: CPU before=" +
                DescribeCpuSlots(chars));

            var pool = new List<CharacterInfoDataTable.CharacterInfoData>();
            for (int i = 0; i < total; i++)
            {
                CharacterInfoDataTable.CharacterInfoData info =
                    cd.getCharacterInfoFromSorted(i);
                if (info == null) continue;
                if (!cd.isUsableCharacter(info)) continue;
                if (cd.getCharacterNpcAiInfo(info.charKey, aiType) == null)
                    continue;
                pool.Add(info);
            }
            if (pool.Count == 0)
            {
                for (int i = 0; i < total; i++)
                {
                    CharacterInfoDataTable.CharacterInfoData info =
                        cd.getCharacterInfoFromSorted(i);
                    if (info == null || !cd.isUsableCharacter(info))
                        continue;
                    if (HasAnyAi(cd, info)) pool.Add(info);
                }
            }
            if (pool.Count == 0)
            {
                for (int i = 0; i < total; i++)
                {
                    CharacterInfoDataTable.CharacterInfoData info =
                        cd.getCharacterInfoFromSorted(i);
                    if (info == null) continue;
                    if (HasAnyAi(cd, info)) pool.Add(info);
                }
            }
            Plugin.Log.LogInfo("Multiplayer: random CPU pool=" +
                pool.Count);
            if (pool.Count == 0) return;

            var used = new HashSet<int>();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i].controlType == BattleDef.CONTROL_TYPE.COM)
                    continue;
                CharacterDataManager.CharacterID id = chars[i].characterId;
                if (id.characterId == 0xFF) continue;
                used.Add(CharacterKey(id));
            }

            int count = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i].controlType != BattleDef.CONTROL_TYPE.COM)
                    continue;
                CharacterInfoDataTable.CharacterInfoData pick = null;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    CharacterInfoDataTable.CharacterInfoData candidate =
                        pool[UnityEngine.Random.Range(0, pool.Count)];
                    if (used.Contains(CharacterKey(
                        CharacterDataManager.getCharacterId(candidate))))
                        continue;
                    pick = candidate;
                    break;
                }
                if (pick == null) continue;
                CharacterDataManager.CharacterID id =
                    CharacterDataManager.getCharacterId(pick);
                used.Add(CharacterKey(id));
                chars[i].characterId = id;
                chars[i].statusId = pick.normalStatusId;
                chars[i].topKnotId =
                    CharacterDataManager.TopKnotID.INVALID_ID;
                count++;
            }

            if (count > 0)
            {
                gs.param.characterParams = chars;
                Plugin.Log.LogInfo("Multiplayer: randomized " + count +
                    " CPU character slots");
                Plugin.Log.LogInfo("Multiplayer: CPU after=" +
                    DescribeCpuSlots(chars));
            }
            else
            {
                Plugin.Log.LogWarning(
                    "Multiplayer: no CPU slots randomized");
            }
        }

        internal static string DescribeCpuSlots(
            MoveSceneParam.CharacterParam[] chars)
        {
            CharacterDataManager cd =
                SingletonBase<CharacterDataManager>.getInstance();
            string text = "";
            if (chars == null || cd == null) return text;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i].controlType != BattleDef.CONTROL_TYPE.COM)
                    continue;
                CharacterInfoDataTable.CharacterInfoData info =
                    cd.getCharacterInfo(chars[i].characterId);
                text += (text.Length == 0 ? "" : " ") +
                    (info == null ? "?" : info.charKey);
            }
            return text;
        }

        private static bool HasAnyAi(CharacterDataManager cd,
            CharacterInfoDataTable.CharacterInfoData info)
        {
            return cd.getCharacterNpcAiInfo(info.charKey,
                       CharacterDataManager.NPC_AI_TYPE.WEAK) != null ||
                   cd.getCharacterNpcAiInfo(info.charKey,
                       CharacterDataManager.NPC_AI_TYPE.NORMAL) != null ||
                   cd.getCharacterNpcAiInfo(info.charKey,
                       CharacterDataManager.NPC_AI_TYPE.STRONG) != null;
        }

        private static int CharacterKey(
            CharacterDataManager.CharacterID id)
        {
            return ((int)id.contentType << 8) | id.characterId;
        }
    }

    [HarmonyPatch(typeof(NPCAIData), "createDesires")]
    internal static class CpuSkillDesirePatch
    {
        private static readonly FieldInfo DefaultVal =
            AccessTools.Field(typeof(NPCDesire), "_defaultVal");
        private static readonly FieldInfo TimeCoefficient =
            AccessTools.Field(typeof(NPCDesire), "_timeCoefficient");
        private static readonly FieldInfo Priority =
            AccessTools.Field(typeof(NPCDesire), "_priority");
        private static readonly FieldInfo StartUpdate =
            AccessTools.Field(typeof(NPCDesire), "_startUpdateCount");
        private static readonly FieldInfo UpdateInterval =
            AccessTools.Field(typeof(NPCDesire), "_updateInterval");

        private static void Postfix(NPC npc, NPCDesire[] __result)
        {
            if (!Plugin.CpuSpecialAbilities.Value) return;
            if (SceneManager.GetActiveScene().name != "LocalBattle")
                return;
            if (npc == null || npc.player == null ||
                npc.player.status == null)
                return;
            if (!npc.player.status.haveSpecialSkill) return;
            if (__result == null) return;

            for (int i = 0; i < __result.Length; i++)
            {
                NPCDesire desire = __result[i];
                if (desire == null || desire.stateName != "useSkill")
                    continue;
                DefaultVal.SetValue(desire, 0.55f);
                TimeCoefficient.SetValue(desire, 0.08f);
                Priority.SetValue(desire, 6);
                StartUpdate.SetValue(desire, 0);
                UpdateInterval.SetValue(desire, 3);
            }
        }
    }

    [HarmonyPatch(typeof(NPCProcessSpecialSkill), MethodType.Constructor,
        typeof(INPCCondition), typeof(AbstractNPCProcess[]))]
    internal static class CpuSkillConditionPatch
    {
        private static readonly FieldInfo ConditionField =
            AccessTools.Field(typeof(AbstractNPCProcess), "_condition");

        private static void Postfix(NPCProcessSpecialSkill __instance)
        {
            if (!Plugin.CpuSpecialAbilities.Value) return;
            if (SceneManager.GetActiveScene().name != "LocalBattle")
                return;
            ConditionField.SetValue(__instance, new NPCConditionTrue());
        }
    }

    [HarmonyPatch(typeof(OfflineReadyScene), "entryPreMoveScene")]
    internal static class RandomCpuReadyResetPatch
    {
        private static void Postfix()
        {
            RandomCpuOpponentPatch.ResetGuard();
        }
    }

    [HarmonyPatch(typeof(LocalBattleScene), "mainStart")]
    internal static class RandomCpuFinalLogPatch
    {
        private static void Postfix()
        {
            if (!Plugin.RandomCpuOpponent.Value) return;
            GameSceneManager gs =
                SingletonBaseWithGameObject<GameSceneManager>.getInstance();
            if (gs == null) return;
            Plugin.Log.LogInfo("Multiplayer: battle CPU=" +
                RandomCpuOpponentPatch.DescribeCpuSlots(
                    gs.param.characterParams));
        }
    }

    [HarmonyPatch(typeof(CharacterDataManager), "isUsableCharacterInternal")]
    internal static class PlatformUsableCharacterPatch
    {
        private static bool Prefix(
            CharacterInfoDataTable.CharacterInfoData __0, bool __1,
            ref bool __result)
        {
            if (!Plugin.PlatformExclusives.Value || __0 == null)
                return true;
            if (!PlatformCharacters.IsExclusive(__0.charKey))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(CharacterDataManager), "isUsableBuiltinCharacter")]
    internal static class PlatformUsableBuiltinPatch
    {
        private static bool Prefix(string __0, bool __1, ref bool __result)
        {
            if (!Plugin.PlatformExclusives.Value) return true;
            if (!PlatformCharacters.IsExclusiveReference(__0))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(AbstractMainMenuScene), "moveToOption")]
    internal static class MainMenuOptionPatch
    {
        private static AbstractMainMenuScene _scene;
        private static bool _bypass;

        private static bool Prefix(AbstractMainMenuScene __instance)
        {
            if (!Plugin.MainMenuQoL.Value) return true;
            if (_bypass)
            {
                _bypass = false;
                return true;
            }
            _scene = __instance;
            PracticeMenu.OpenFromMenu();
            return false;
        }

        internal static void OpenOriginalOptions()
        {
            if (_scene == null) return;
            PracticeMenu.CloseFromMenu();
            _bypass = true;
            _scene.moveToOption();
        }
    }

    [HarmonyPatch(typeof(AbstractMainMenuUI), "init")]
    internal static class MainMenuLabelPatch
    {
        private static void Prefix()
        {
            MainMenuQoLUtility.Apply();
        }
    }

    [HarmonyPatch(typeof(InputAdapter), "getEvent")]
    internal static class PracticeInputPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!PracticeMenu.Visible) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveDataManager), "startStorySave")]
    internal static class PracticeSavePatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Practice.Active) return true;
            __result = false;
            Plugin.Log.LogInfo("Practice: suppressed story save");
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveDataManager), "startSaveAll")]
    internal static class PracticeSaveAllPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!Practice.Active) return true;
            __result = false;
            Plugin.Log.LogInfo("Practice: suppressed full save");
            return false;
        }
    }

    internal static class TitleLogosPatch
    {
        internal static FieldInfo LogosField;

        private static void Prefix(TitleUI __instance)
        {
            if (!Plugin.AutoSkipLogos.Value) return;
            try
            {
                Type elem = LogosField.FieldType.GetElementType();
                LogosField.SetValue(__instance,
                    Array.CreateInstance(elem, 0));
                Plugin.Log.LogInfo("Publisher logo states removed");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Logo removal failed: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(InputAdapter), "getEvent")]
    internal static class InputAdapterSkipPatch
    {
        private static float _lastLog;

        private static void Postfix(InputAdapter.EVENT e, ref bool __result)
        {
            if (__result) return;
            int ev = (int)e;
            if (ev != 0x15 && ev != 0x0b) return; // UI_ANY, UI_SUBMIT
            if (!Plugin.SkipEnabled.Value) return;
            if (ev == 0x0b && !Plugin.MovieActive()) return;
            string source;
            if (!Plugin.SkipPressed(out source)) return;
            __result = true;
            if (Time.unscaledTime - _lastLog > 1f)
            {
                _lastLog = Time.unscaledTime;
                Plugin.Log.LogInfo((ev == 0x0b ? "UI_SUBMIT" : "UI_ANY") +
                    " injected via " + source);
            }
        }
    }

    internal static class LogoSkipPatch
    {
        internal static FieldInfo SkippableField;
        internal static FieldInfo AnimField;
        internal static FieldInfo SkippedField;
        internal static FieldInfo NameField;
        private static readonly int IsSkipHash =
            Animator.StringToHash("isSkip");

        private static void Prefix(object __instance)
        {
            if (!Plugin.SkipEnabled.Value) return;
            try
            {
                if (Plugin.AutoSkipLogos.Value)
                {
                    Animator anim = AnimField == null ? null :
                        AnimField.GetValue(__instance) as Animator;
                    if (anim != null)
                    {
                        bool already = anim.GetBool(IsSkipHash);
                        anim.SetBool(IsSkipHash, true);
                        if (SkippedField != null)
                            SkippedField.SetValue(__instance, true);
                        if (!already && NameField != null)
                            Plugin.Log.LogInfo("Auto-skipping logo " +
                                NameField.GetValue(__instance));
                    }
                }
                else if (SkippableField != null)
                {
                    SkippableField.SetValue(__instance, true);
                }
            }
            catch { }
        }
    }

    public class SkipWatcher : MonoBehaviour
    {
        private float _lastPress;
        private float _lastAutoScan;
        private float _lastForce;
        private float _lastSoftReset;
        private float _softResetHeld;
        private float _lastPadLog;
        private float _lastAchievementCheck;
        private bool _softResetLatched;
        private bool _masterForced;

        private void Update()
        {
            if (Plugin.UnlockRoomAchievements.Value &&
                Time.unscaledTime - _lastAchievementCheck > 3f)
            {
                _lastAchievementCheck = Time.unscaledTime;
                TrophyManager tm =
                    SingletonBaseWithGameObject<TrophyManager>.getInstance();
                if (tm != null)
                {
                    try
                    {
                        for (int i = 0; i < 100; i++) tm.FuncCreateRoom();
                        Plugin.UnlockRoomAchievements.Value = false;
                        if (Plugin.Instance != null)
                            Plugin.Instance.Config.Save();
                        Plugin.Log.LogInfo(
                            "Room achievements unlocked via FuncCreateRoom x100");
                    }
                    catch (Exception e)
                    {
                        Plugin.Log.LogWarning(
                            "Room achievement helper failed: " + e.Message);
                    }
                }
            }
            if (Plugin.SoftResetEnabled.Value)
            {
                float hold = Plugin.SoftResetPadHold.Value;
                if (hold < 0.2f) hold = 0.2f;
                if (hold > 3f) hold = 3f;
                if (Plugin.SoftResetDebug.Value &&
                    Plugin.SoftResetDebugHeld() &&
                    Time.unscaledTime - _lastPadLog > 1f)
                {
                    _lastPadLog = Time.unscaledTime;
                    Plugin.LogSoftResetPadState();
                }
                if (Plugin.SoftResetComboHeld())
                {
                    if (!_softResetLatched)
                    {
                        _softResetHeld += Time.unscaledDeltaTime;
                        if (_softResetHeld >= hold &&
                            Time.unscaledTime - _lastSoftReset > 3f)
                        {
                            _lastSoftReset = Time.unscaledTime;
                            _softResetHeld = 0f;
                            _softResetLatched = true;
                            Plugin.DoSoftReset("pad combo");
                            return;
                        }
                    }
                }
                else
                {
                    _softResetHeld = 0f;
                    _softResetLatched = false;
                }
                if (!_softResetLatched && Plugin.SoftResetKeyPressed() &&
                    Time.unscaledTime - _lastSoftReset > 3f)
                {
                    _lastSoftReset = Time.unscaledTime;
                    Plugin.DoSoftReset("keyboard");
                    return;
                }
            }
            if (Plugin.ForceMasterEnv && !_masterForced &&
                Time.unscaledTime - _lastForce > 1f)
            {
                _lastForce = Time.unscaledTime;
                if (Plugin.TryForceMaster()) _masterForced = true;
            }
            AutoSkipMovies();
            if (!Plugin.SkipEnabled.Value) return;
            string source;
            if (!Plugin.SkipPressed(out source)) return;
            if (Time.unscaledTime - _lastPress < 0.5f) return;
            _lastPress = Time.unscaledTime;

            bool playing = false;
            UnityEngine.Object[] videos =
                Resources.FindObjectsOfTypeAll(typeof(VideoPlayer));
            for (int i = 0; i < videos.Length; i++)
            {
                VideoPlayer video = videos[i] as VideoPlayer;
                if (video == null || !video.isPlaying) continue;
                playing = true;
                if (Plugin.SkipVideo(video, source)) return;
            }

            UnityEngine.Object[] scenes =
                Resources.FindObjectsOfTypeAll(typeof(AbstractMoviePlayScene));
            for (int i = 0; i < scenes.Length; i++)
            {
                AbstractMoviePlayScene scene =
                    scenes[i] as AbstractMoviePlayScene;
                if (scene == null || !scene.isActiveAndEnabled) continue;
                Plugin.Log.LogInfo("Skipping movie scene " +
                    scene.GetType().Name + " via " + source);
                scene.moveScene();
                return;
            }

            Plugin.Log.LogInfo("Skip input (" + source + "): " +
                (playing ? "movie not seekable" : "no movie scene found"));
        }

        private void AutoSkipMovies()
        {
            float seconds = Plugin.AutoSkipMovies.Value;
            if (seconds <= 0f) return;
            if (Time.unscaledTime - _lastAutoScan < 0.5f) return;
            _lastAutoScan = Time.unscaledTime;
            UnityEngine.Object[] videos =
                Resources.FindObjectsOfTypeAll(typeof(VideoPlayer));
            for (int i = 0; i < videos.Length; i++)
            {
                VideoPlayer video = videos[i] as VideoPlayer;
                if (video == null || !video.isPlaying) continue;
                if (video.time < seconds) continue;
                Plugin.SkipVideo(video, "auto-skip " + seconds + "s");
            }
        }
    }

    [HarmonyPatch(typeof(PostProcessingBehaviour), "OnEnable")]
    internal static class PostFxPatch
    {
        private static bool _logged;
        private static void Prefix(PostProcessingBehaviour __instance)
        {
            Apply(__instance);
        }

        internal static void Apply(PostProcessingBehaviour behaviour)
        {
            PostProcessingProfile p = behaviour.profile;
            if (p == null) return;
            if (!_logged)
            {
                Plugin.Log.LogInfo("Post-processing profile patched");
                _logged = true;
            }

            if (Plugin.NoMotionBlur.Value && p.motionBlur != null)
                p.motionBlur.enabled = false;
            if (Plugin.NoDepthOfField.Value && p.depthOfField != null)
                p.depthOfField.enabled = false;
            if (Plugin.NoBloom.Value && p.bloom != null)
                p.bloom.enabled = false;
            if (Plugin.NoVignette.Value && p.vignette != null)
                p.vignette.enabled = false;
            if (Plugin.NoGrain.Value && p.grain != null)
                p.grain.enabled = false;
        }
    }
}
