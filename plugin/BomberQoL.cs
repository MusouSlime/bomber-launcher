using System;
using System.Collections;
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
    [BepInPlugin(GUID, "Bomber QoL", "0.3.3")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "bomber.qol";

        internal static ManualLogSource Log;
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
        internal static ConfigEntry<string> PracticeMenuKeyName;
        internal static ConfigEntry<string> PracticeMenuPadCombo;
        internal static ConfigEntry<float> PracticeMenuPadHold;
        internal static ConfigEntry<bool> PracticeMenuDebug;
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
            PracticeMenuKeyName = Config.Bind("Practice", "PracticeMenuKey", "F9",
                "Key used to open the in-game practice stage selector.");
            PracticeMenuPadCombo = Config.Bind("Practice", "PracticeMenuPadCombo", "4,5,6",
                "Controller combo for the practice selector: 4=L1/LB, 5=R1/RB, 6=Select/Back. Default L1+R1+Select.");
            PracticeMenuPadHold = Config.Bind("Practice", "PracticeMenuPadHold", 0.6f,
                "Seconds the practice-selector combo must be held (0.2 - 3.0).");
            PracticeMenuDebug = Config.Bind("Practice", "PracticeMenuDebug", false,
                "Log practice-selector button states while its combo is held (diagnostics).");
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
                PracticeMenuPadButtons = new[] { 4, 5, 6 };
                Log.LogWarning("Invalid PracticeMenuPadCombo; using 4,5,6 (L1+R1+Select).");
            }
            string combo = "";
            for (int i = 0; i < PracticeMenuPadButtons.Length; i++)
            {
                if (i > 0) combo += ",";
                combo += PracticeMenuPadButtons[i];
            }
            Log.LogInfo("Practice combo: " + combo +
                " hold " + PracticeMenuPadHold.Value + "s");

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(PostFxPatch));
            harmony.PatchAll(typeof(InputAdapterSkipPatch));
            harmony.PatchAll(typeof(PracticeSavePatch));
            harmony.PatchAll(typeof(PracticeSaveAllPatch));
            harmony.PatchAll(typeof(PracticeInputPatch));

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

        internal static void DoSoftReset(string source)
        {
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

    internal static class Practice
    {
        internal static bool Active;

        internal static void OnSceneLoaded(Scene scene)
        {
            if (scene.name == "Title" || scene.name == "MainMenu")
                Active = false;
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

            Active = true;
            Plugin.Log.LogInfo("Practice: world " + world + " stage " + stage +
                " (" + difficulty + "), story saves suppressed");
            gs.moveScene("StageSetup");
            return true;
        }
    }

    public class PracticeMenu : MonoBehaviour
    {
        internal static bool Visible;
        private static readonly string[] Difficulties =
            { "easy", "normal", "hard", "super" };
        private static readonly string[] DiffLabels =
            { "Easy", "Normal", "Hard", "Master" };
        private bool _visible;
        private bool _prevCursorVisible;
        private CursorLockMode _prevLockMode;
        private int _world = 1;
        private int _stage = 1;
        private int _difficulty = 1;
        private int _row;
        private bool _leftHeld;
        private bool _rightHeld;
        private bool _upHeld;
        private bool _downHeld;
        private float _comboHeld;
        private float _lastPadLog;
        private bool _comboLatched;

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
            HandleNavigation();
        }

        private void LateUpdate()
        {
            if (!_visible) return;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private static bool PadButtonDown(string name)
        {
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
            if (adapter == null) return 0f;
            try { return adapter.GetAxis(name + "_1P"); }
            catch { return 0f; }
        }

        private void HandleNavigation()
        {
            float ax = PadAxis("CROSS_HORIZONTAL");
            float ay = PadAxis("CROSS_VERTICAL");
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

            if (left || right)
            {
                int delta = left ? -1 : 1;
                if (_row == 0)
                    _world = Mathf.Clamp(_world + delta, 1, 7);
                else if (_row == 1)
                    _stage = Mathf.Clamp(_stage + delta, 1, 10);
                else if (_row == 2)
                    _difficulty = Mathf.Clamp(_difficulty + delta, 0,
                        DiffLabels.Length - 1);
            }
            if (up) _row = (_row + 3) % 4;
            if (down) _row = (_row + 1) % 4;

            bool accept = Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                PadButtonDown("A");
            bool cancel = Input.GetKeyDown(KeyCode.Escape) ||
                PadButtonDown("B");
            if (cancel) { SetVisible(false); return; }
            if (accept &&
                Practice.Start(_world, _stage, Difficulties[_difficulty]))
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

        private void OnGUI()
        {
            if (!_visible) return;
            GUI.depth = -1000;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            const int w = 570;
            const int h = 390;
            Rect r = new Rect((Screen.width - w) / 2,
                (Screen.height - h) / 2, w, h);
            GUI.Box(r, "Practice Stage");
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 30, w - 32, h - 46));

            GUILayout.Label((_row == 0 ? "> " : "  ") + "World");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 7; i++)
            {
                if (GUILayout.Button(i.ToString(),
                    GUILayout.Width(56), GUILayout.Height(28)))
                    _world = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label((_row == 1 ? "> " : "  ") + "Stage");
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= 10; i++)
            {
                if (GUILayout.Button(i.ToString(),
                    GUILayout.Width(44), GUILayout.Height(28)))
                    _stage = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label((_row == 2 ? "> " : "  ") + "Difficulty");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < DiffLabels.Length; i++)
            {
                if (GUILayout.Button(DiffLabels[i],
                    GUILayout.Width(92), GUILayout.Height(28)))
                    _difficulty = i;
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("World " + _world + ", stage " + _stage + "  —  " +
                DiffLabels[_difficulty] + "  (story saves disabled)");
            GUILayout.Label(_row == 3
                ? "> Enter / A: start practice    B / Esc: close"
                : "  Enter / A: start practice    B / Esc: close");
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Practice", GUILayout.Height(34)))
            {
                if (Practice.Start(_world, _stage, Difficulties[_difficulty]))
                    SetVisible(false);
            }
            if (GUILayout.Button("Close", GUILayout.Height(34)))
                SetVisible(false);
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
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
        private bool _softResetLatched;
        private bool _masterForced;

        private void Update()
        {
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
