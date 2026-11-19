using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.PostProcessing;
using UnityEngine.Video;

namespace BomberQoL
{
    [BepInPlugin(GUID, "Bomber QoL", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "bomber.qol";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> SkipEnabled;
        internal static ConfigEntry<string> SkipKeyName;
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
            harmony.PatchAll(typeof(MovieSkipPatch));
            harmony.PatchAll(typeof(PostFxPatch));

            Log.LogInfo("Bomber QoL loaded (skip key: " + SkipKey + ")");
        }
    }

    [HarmonyPatch(typeof(AbstractMoviePlayScene), "mainUpdate")]
    internal static class MovieSkipPatch
    {
        private static readonly FieldInfo VideoField =
            AccessTools.Field(typeof(AbstractMoviePlayScene), "_video");

        private static void Prefix(AbstractMoviePlayScene __instance)
        {
            if (!Plugin.SkipEnabled.Value) return;
            if (!Input.GetKeyDown(Plugin.SkipKey)) return;

            VideoPlayer video = VideoField == null ? null :
                VideoField.GetValue(__instance) as VideoPlayer;
            if (video == null || !video.isPlaying) return;
            if (video.frameRate <= 0f || video.frameCount == 0) return;

            double target = (double)video.frameCount / video.frameRate - 0.05;
            if (target < 0.0) target = 0.0;
            if (video.time < target)
            {
                video.time = target;
                Plugin.Log.LogInfo("Skipping movie via " + Plugin.SkipKey);
            }
        }
    }

    [HarmonyPatch(typeof(PostProcessingBehaviour), "OnEnable")]
    internal static class PostFxPatch
    {
        private static void Prefix(PostProcessingBehaviour __instance)
        {
            PostProcessingProfile p = __instance.profile;
            if (p == null) return;

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
