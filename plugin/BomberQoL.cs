using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace BomberQoL
{
    [BepInPlugin(GUID, "Bomber QoL", "0.1.3")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "bomber.qol";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> SkipEnabled;
        internal static ConfigEntry<string> SkipKeyName;
        internal static ConfigEntry<int> SkipJoystickButton;
        internal static ConfigEntry<bool> NoMotionBlur;
        internal static ConfigEntry<bool> NoDepthOfField;
        internal static ConfigEntry<bool> NoBloom;
        internal static ConfigEntry<bool> NoVignette;
        internal static ConfigEntry<bool> NoGrain;
        internal static KeyCode SkipKey = KeyCode.Space;

        private void Awake()
        {
            Log = Logger;

            SkipEnabled = Config.Bind("Movies", "SkipEnabled", true,
                "Allow skipping cutscenes and the boot intro with a key.");
            SkipKeyName = Config.Bind("Movies", "SkipKey", "Space",
                "Unity KeyCode used to skip a playing movie (Space, Escape, ...).");
            SkipJoystickButton = Config.Bind("Movies", "SkipJoystickButton", 1,
                "Joystick button used to skip a playing movie: 0=A/Cross, 1=B/Circle, 2=X/Square, 3=Y/Triangle, 4=LB, 5=RB, 6=Back, 7=Start; -1 disables.");

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
                harmony.Patch(logoExec, prefix: new HarmonyMethod(
                    typeof(LogoSkipPatch).GetMethod("Prefix",
                        BindingFlags.Static | BindingFlags.NonPublic)));
                Log.LogInfo("Logo skip patch applied");
            }
            else
            {
                Log.LogWarning("LogoState patch target not found");
            }

            var watcher = new GameObject("BomberQoLSkipper");
            UnityEngine.Object.DontDestroyOnLoad(watcher);
            watcher.AddComponent<SkipWatcher>();

            SceneManager.sceneLoaded += OnSceneLoaded;
            Log.LogInfo("Bomber QoL loaded (skip key: " + SkipKey +
                        ", pad button: " + SkipJoystickButton.Value + ")");
        }

        private void Start()
        {
            ApplyAllPostFx();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyAllPostFx();
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

        internal static bool SkipPressed(out string source)
        {
            if (Input.GetKeyDown(SkipKey))
            {
                source = SkipKey.ToString();
                return true;
            }
            int button = SkipJoystickButton.Value;
            if (button >= 0 && button <= 19)
            {
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
            }
            source = null;
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
    }

    [HarmonyPatch(typeof(InputAdapter), "getEvent")]
    internal static class InputAdapterSkipPatch
    {
        private static float _lastLog;

        private static void Postfix(InputAdapter.EVENT e, ref bool __result)
        {
            if (__result) return;
            if ((int)e != 0x15) return;   // InputAdapter.EVENT.UI_ANY
            if (!Plugin.SkipEnabled.Value) return;
            string source;
            if (!Plugin.SkipPressed(out source)) return;
            __result = true;
            if (Time.unscaledTime - _lastLog > 1f)
            {
                _lastLog = Time.unscaledTime;
                Plugin.Log.LogInfo("UI_ANY injected via " + source);
            }
        }
    }

    internal static class LogoSkipPatch
    {
        internal static FieldInfo SkippableField;

        private static void Prefix(object __instance)
        {
            if (!Plugin.SkipEnabled.Value) return;
            try
            {
                if (SkippableField != null)
                    SkippableField.SetValue(__instance, true);
            }
            catch { }
        }
    }

    public class SkipWatcher : MonoBehaviour
    {
        private float _lastPress;

        private void Update()
        {
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
