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
        internal static ConfigEntry<string> CustomBgmSource;
        internal static ConfigEntry<float> CustomBgmVolume;
        internal static ConfigEntry<bool> CustomBgmShuffle;
        internal static ConfigEntry<bool> CustomBgmPlaylist;
        internal static ConfigEntry<string> ExplosionSfxFolder;
        internal static ConfigEntry<string> ExplosionSfxLabels;
        internal static ConfigEntry<float> ExplosionSfxVolume;
        internal static ConfigEntry<bool> ExplosionSfxShuffle;
        internal static ConfigEntry<bool> SelfTest;

        // Performance
        internal static ConfigEntry<bool> LowEnd;
        internal static ConfigEntry<bool> UltraLow;
        internal static ConfigEntry<bool> HighEnd;
        internal static ConfigEntry<int> LowEndFps;
        internal static ConfigEntry<float> UltraRenderScale;
        internal static ConfigEntry<int> HighEndFps;
        internal static ConfigEntry<bool> HighEndVSync;

        // Story
        internal static ConfigEntry<string> StoryCharacterKey;

        // Interface
        internal static ConfigEntry<bool> OverlayEnabled;
        internal static ConfigEntry<string> OverlayKeyName;
        internal static ConfigEntry<bool> ReplaceOptionsMenu;
        internal static ConfigEntry<bool> GamepadNavigation;

        internal static KeyCode SkipKey = KeyCode.Space;
        internal static KeyCode OverlayKey = KeyCode.F9;

        internal static void SaveConfig()
        {
            try
            {
                if (Instance != null) Instance.Config.Save();
            }
            catch (Exception e)
            {
                Logger.LogWarning("Config save failed: " + e.Message);
            }
        }

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
            QoLStatic.Init();
            PerformanceTiers.Apply();

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
            SkipLogos = Config.Bind("Movies", "SkipLogos", false,
                "Skip the publisher/logo splash at boot. Disabled for SBR2 until a safe skip path is implemented.");
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
            CustomBgmSource = Config.Bind("Audio", "CustomBgmSource", "",
                "Original music folder chosen in the launcher (display only).");
            CustomBgmVolume = Config.Bind("Audio", "CustomBgmVolume", 0.8f,
                new ConfigDescription("Custom BGM volume.",
                    new AcceptableValueRange<float>(0f, 1f)));
            CustomBgmShuffle = Config.Bind("Audio", "CustomBgmShuffle", false,
                "When a BGM label has no matching file, play a random track.");
            CustomBgmPlaylist = Config.Bind("Audio", "CustomBgmPlaylist", true,
                "When a BGM label has no matching file, play the folder in order.");
            ExplosionSfxFolder = Config.Bind("Audio", "ExplosionSfxFolder", "",
                "Folder of .ogg/.wav explosion clips; empty disables replacement.");
            ExplosionSfxLabels = Config.Bind("Audio", "ExplosionSfxLabels",
                "bom,exp,fire,blast",
                "Comma-separated label fragments treated as explosions.");
            ExplosionSfxVolume = Config.Bind("Audio", "ExplosionSfxVolume", 0.8f,
                new ConfigDescription("Explosion SFX volume.",
                    new AcceptableValueRange<float>(0f, 1f)));
            ExplosionSfxShuffle = Config.Bind("Audio", "ExplosionSfxShuffle", true,
                "Pick a random explosion clip instead of cycling in order.");

            SelfTest = Config.Bind("Debug", "SelfTest", false,
                "Play the first custom BGM track once at startup to verify the audio path.");

            LowEnd = Config.Bind("Performance", "LowEnd", false,
                "Low-End floor: half textures, no shadows, no pixel lights, 60 FPS.");
            UltraLow = Config.Bind("Performance", "UltraLow", false,
                "Ultra-Low floor: half render scale, quarter textures, no shadows/lights.");
            HighEnd = Config.Bind("Performance", "HighEnd", false,
                "High-End ceiling; overrides LowEnd/UltraLow.");
            LowEndFps = Config.Bind("Performance", "LowEndFps", 60,
                "Frame cap for Low-End and Ultra-Low (0 = uncapped).");
            UltraRenderScale = Config.Bind("Performance", "UltraRenderScale", 0.5f,
                new ConfigDescription("Ultra-Low render scale.",
                    new AcceptableValueRange<float>(0.25f, 1f)));
            HighEndFps = Config.Bind("Performance", "HighEndFps", 0,
                "High-End frame cap (0 = uncapped).");
            HighEndVSync = Config.Bind("Performance", "HighEndVSync", true,
                "High-End VSync.");

            OverlayEnabled = Config.Bind("Interface", "OverlayEnabled", true,
                "Show the F9 settings overlay.");
            OverlayKeyName = Config.Bind("Interface", "OverlayKey", "F9",
                "Unity KeyCode that toggles the settings overlay.");
            ReplaceOptionsMenu = Config.Bind("Interface", "ReplaceOptionsMenu",
                true, "Open the QoL overlay instead of the game's basic options menu.");
            GamepadNavigation = Config.Bind("Interface", "GamepadNavigation",
                true, "Allow the QoL overlay to read the game controller.");

            StoryCharacterKey = Config.Bind("Story", "StoryCharacter", "",
                "Story-mode player character key: white, black, pink, blue, red, " +
                "yellow, green, aqua, simon, pyramid, richter, lance, bill, mimi, " +
                "nyami, alucard, vicviper, nurse, lobby, tomato, nakedSnake, " +
                "raiden, oldsnake, vicviperzero, somaCruz, fujisaki, genso2p, " +
                "goemon, magnet, tinyGolem, phantom, sexy, plasma, fiter, " +
                "Massive, wing, bean, lucaBlight. Empty = vanilla White.");

            if (!Enum.TryParse<KeyCode>(OverlayKeyName.Value, true,
                out OverlayKey))
            {
                OverlayKey = KeyCode.F9;
                Logger.LogWarning("Invalid OverlayKey; using F9.");
            }
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
