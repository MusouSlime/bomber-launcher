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
    [BepInPlugin(GUID, "Bomber QoL", "0.1.9")]
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
        internal static ConfigEntry<bool> FastResults;
        internal static ConfigEntry<float> ResultSpeed;
        internal static ConfigEntry<bool> NoMotionBlur;
        internal static ConfigEntry<bool> NoDepthOfField;
        internal static ConfigEntry<bool> NoBloom;
        internal static ConfigEntry<bool> NoVignette;
        internal static ConfigEntry<bool> NoGrain;
        internal static KeyCode SkipKey = KeyCode.Space;
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

            var harmony = new Harmony(GUID);
            harmony.PatchAll(typeof(PostFxPatch));
            harmony.PatchAll(typeof(InputAdapterSkipPatch));

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

            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("Bomber QoL loaded (skip key: " + SkipKey +
                        ", pad button: " + SkipJoystickButton.Value + ")");
            StartCoroutine(EnableMasterSoon());
        }

        private void Start()
        {
            ApplyAllPostFx();
            ApplyResultTweaks();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyAllPostFx();
            ApplyResultTweaks();
            EnableMasterButton();
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
        private bool _masterForced;

        private void Update()
        {
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
