using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace SBR2QoL
{
    [BepInPlugin(GUID, "SBR2 QoL", "0.1.0")]
    public class Plugin : BasePlugin
    {
        public const string GUID = "sbr2.qol";

        internal static Plugin Instance;
        internal static ManualLogSource Logger;

        // Movies / logos
        internal static ConfigEntry<bool> SkipMovies;
        internal static ConfigEntry<string> SkipKeyName;
        internal static ConfigEntry<bool> SkipLogos;
        internal static ConfigEntry<float> AutoSkipMovies;

        // Graphics
        internal static ConfigEntry<bool> DisableMotionBlur;
        internal static ConfigEntry<bool> DisableDepthOfField;
        internal static ConfigEntry<bool> DisableBloom;
        internal static ConfigEntry<bool> DisableVignette;
        internal static ConfigEntry<bool> DisableFilmGrain;
        internal static ConfigEntry<bool> DisableChromaticAberration;
        internal static ConfigEntry<bool> DisableLensDistortion;
        internal static ConfigEntry<float> FieldOfView;

        // Audio
        internal static ConfigEntry<string> CustomBgmFolder;
        internal static ConfigEntry<float> CustomBgmVolume;
        internal static ConfigEntry<bool> CustomBgmShuffle;
        internal static ConfigEntry<bool> CustomBgmPlaylist;

        // Story
        internal static ConfigEntry<string> StoryCharacterKey;

        internal static KeyCode SkipKey = KeyCode.Space;

        public override void Load()
        {
            Instance = this;
            Logger = Log;

            BindConfig();

            // SBR2's Il2CppInterop build currently fails ClassInjector for
            // managed MonoBehaviours. Keep this plugin Harmony-only until a
            // working loader path is selected; injecting QoLHost here crashes
            // the process with an access violation.
            PatchAllSafe();

            Logger.LogInfo("SBR2 QoL loaded (story character='" +
                StoryCharacterKey.Value + "', BGM folder='" +
                CustomBgmFolder.Value + "')");
        }

        private void BindConfig()
        {
            SkipMovies = Config.Bind("Movies", "SkipEnabled", true,
                "Allow skipping cutscenes with the skip key.");
            SkipKeyName = Config.Bind("Movies", "SkipKey", "Space",
                "Unity KeyCode used to skip a playing movie (Space, Escape, ...).");
            SkipLogos = Config.Bind("Movies", "SkipLogos", true,
                "Skip the publisher/logo splash at boot.");
            AutoSkipMovies = Config.Bind("Movies", "AutoSkipMovies", 0f,
                new ConfigDescription(
                    "Automatically skip every movie after this many seconds (0 = off).",
                    new AcceptableValueRange<float>(0f, 120f)));

            DisableMotionBlur = Config.Bind("Graphics", "DisableMotionBlur", true,
                "Disable the HDRP motion blur volume component.");
            DisableDepthOfField = Config.Bind("Graphics", "DisableDepthOfField", true,
                "Disable the HDRP depth-of-field volume component.");
            DisableBloom = Config.Bind("Graphics", "DisableBloom", false,
                "Disable the HDRP bloom volume component.");
            DisableVignette = Config.Bind("Graphics", "DisableVignette", false,
                "Disable the HDRP vignette volume component.");
            DisableFilmGrain = Config.Bind("Graphics", "DisableFilmGrain", false,
                "Disable the HDRP film grain volume component.");
            DisableChromaticAberration = Config.Bind("Graphics", "DisableChromaticAberration", false,
                "Disable the HDRP chromatic aberration volume component.");
            DisableLensDistortion = Config.Bind("Graphics", "DisableLensDistortion", false,
                "Disable the HDRP lens distortion volume component.");
            FieldOfView = Config.Bind("Graphics", "FieldOfView", 0f,
                new ConfigDescription(
                    "Override gameplay camera FOV (0 = leave vanilla).",
                    new AcceptableValueRange<float>(0f, 120f)));

            CustomBgmFolder = Config.Bind("Audio", "CustomBgmFolder", "",
                "Folder containing .ogg/.wav BGM files. Empty disables replacement.");
            CustomBgmVolume = Config.Bind("Audio", "CustomBgmVolume", 0.8f,
                new ConfigDescription("Custom BGM volume.",
                    new AcceptableValueRange<float>(0f, 1f)));
            CustomBgmShuffle = Config.Bind("Audio", "CustomBgmShuffle", false,
                "When a BGM label has no matching file, play a random track.");
            CustomBgmPlaylist = Config.Bind("Audio", "CustomBgmPlaylist", false,
                "When a BGM label has no matching file, cycle through the folder.");

            StoryCharacterKey = Config.Bind("Story", "StoryCharacter", "",
                "Story-mode player character key: white, black, pink, blue, red, " +
                "yellow, green, aqua, simon, pyramid, richter, lance, bill, mimi, " +
                "nyami, alucard, vicviper, nurse, lobby, tomato, nakedSnake, " +
                "raiden, oldsnake, vicviperzero, somaCruz, fujisaki, genso2p, " +
                "goemon, magnet, tinyGolem, phantom, sexy, plasma, fiter, " +
                "Massive, wing, bean, lucaBlight. Empty = vanilla White.");

            if (!Enum.TryParse<KeyCode>(SkipKeyName.Value, true, out SkipKey))
            {
                SkipKey = KeyCode.Space;
                Logger.LogWarning("Invalid SkipKey; using Space.");
            }
        }

        private static void PatchAllSafe()
        {
            var harmony = new Harmony(GUID);
            foreach (var type in typeof(Plugin).Assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
                    continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception e)
                {
                    Logger.LogWarning("Patch " + type.Name + " skipped: " + e.Message);
                }
            }
        }
    }
}
