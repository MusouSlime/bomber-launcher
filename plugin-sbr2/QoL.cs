using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime;
using System;
using System.Collections;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils;
using UnityEngine.Rendering;

namespace SBR2QoL
{
    internal static class QoLStatic
    {
        private static SoundManager _host;
        private static bool _running;
        private static float _nextVolume;
        private static float _nextFov;
        private static float _movieSeenAt = -1f;
        private static bool _autoSkipped;

        internal static void Init()
        {
        }

        internal static void EnsureLoop()
        {
            if (_running) return;
            StartOn(UnityEngine.Object.FindObjectOfType<SoundManager>());
        }

        internal static void StartOn(SoundManager host)
        {
            if (_running || host == null) return;
            _host = host;
            _running = true;
            _host.StartCoroutine(Loop());
            Plugin.Logger.LogInfo("SBR2 QoL tick started on SoundManager");
        }

        private static IEnumerator Loop()
        {
            while (true)
            {
                try { Tick(); }
                catch (Exception e)
                {
                    Plugin.Logger.LogWarning("QoL tick: " + e.Message);
                }
                yield return null;
            }
        }

        internal static void Tick()
        {
            UpdateMovieSkip();

            if (Time.unscaledTime >= _nextVolume)
            {
                _nextVolume = Time.unscaledTime + 0.5f;
                ApplyVolumeToggles();
            }
            if (Plugin.FieldOfView.Value > 0f &&
                Time.unscaledTime >= _nextFov)
            {
                _nextFov = Time.unscaledTime + 0.5f;
                ApplyFov();
            }
        }

        private static void UpdateMovieSkip()
        {
            if (!Plugin.SkipMovies.Value) return;
            var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MoviePlayUI>());
            bool active = false;
            for (int i = 0; i < objects.Length; i++)
            {
                var movie = objects[i] as MoviePlayUI;
                if (movie == null || !movie.isActiveAndEnabled) continue;
                active = true;
                if (Input.GetKeyDown(Plugin.SkipKey))
                {
                    movie.onSubmitSkipButton();
                    Plugin.Logger.LogInfo("Skipping movie via key");
                    return;
                }
                if (Plugin.AutoSkipMovies.Value > 0f)
                {
                    if (_movieSeenAt < 0f)
                    {
                        _movieSeenAt = Time.unscaledTime;
                        _autoSkipped = false;
                    }
                    else if (!_autoSkipped &&
                        Time.unscaledTime - _movieSeenAt >= Plugin.AutoSkipMovies.Value)
                    {
                        movie.onSubmitSkipButton();
                        _autoSkipped = true;
                        Plugin.Logger.LogInfo("Skipping movie via auto timer");
                    }
                }
            }
            if (!active)
            {
                _movieSeenAt = -1f;
                _autoSkipped = false;
            }
        }

        private static void ApplyVolumeToggles()
        {
            var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Volume>());
            for (int i = 0; i < objects.Length; i++)
            {
                var volume = objects[i] as Volume;
                if (volume == null || volume.profile == null) continue;
                var components = volume.profile.components;
                if (components == null) continue;
                for (int j = 0; j < components.Count; j++)
                {
                    var component = components[j];
                    if (component == null) continue;
                    string name = component.GetType().Name;
                    bool disable =
                        (Plugin.DisableMotionBlur.Value && name == "MotionBlur") ||
                        (Plugin.DisableDepthOfField.Value && name == "DepthOfField") ||
                        (Plugin.DisableBloom.Value && name == "Bloom") ||
                        (Plugin.DisableVignette.Value && name == "Vignette") ||
                        (Plugin.DisableFilmGrain.Value && name == "FilmGrain") ||
                        (Plugin.DisableChromaticAberration.Value && name == "ChromaticAberration") ||
                        (Plugin.DisableLensDistortion.Value && name == "LensDistortion");
                    if (disable && component.active)
                        component.active = false;
                }
            }
        }

        private static void ApplyFov()
        {
            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || cam.orthographic) continue;
                if (Mathf.Abs(cam.fieldOfView - Plugin.FieldOfView.Value) > 0.01f)
                    cam.fieldOfView = Plugin.FieldOfView.Value;
            }
        }
    }



    [HarmonyPatch(typeof(SoundManager), "Update")]
    internal static class QoLSoundUpdatePatch
    {
        private static bool _logged;
        private static void Postfix()
        {
            if (!_logged)
            {
                _logged = true;
                Plugin.Logger.LogInfo("SBR2 QoL tick active (SoundManager.Update)");
            }
            QoLStatic.Tick();
        }
    }

    public class QoLHost : MonoBehaviour
    {
        private float _nextVolumeCheck;
        private float _nextFovCheck;
        private float _movieSeenAt = -1f;
        private bool _autoSkipped;

        private void Update()
        {
            if (Plugin.SkipMovies.Value)
                UpdateMovieSkip();

            if (Time.unscaledTime >= _nextVolumeCheck)
            {
                ApplyVolumeToggles();
                _nextVolumeCheck = Time.unscaledTime + 0.5f;
            }

            if (Plugin.FieldOfView.Value > 0f &&
                Time.unscaledTime >= _nextFovCheck)
            {
                ApplyFov();
                _nextFovCheck = Time.unscaledTime + 0.5f;
            }
        }

        private void UpdateMovieSkip()
        {
            var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MoviePlayUI>());
            bool active = false;

            for (int i = 0; i < objects.Length; i++)
            {
                var movie = objects[i] as MoviePlayUI;
                if (movie == null || !movie.isActiveAndEnabled) continue;
                active = true;

                if (Input.GetKeyDown(Plugin.SkipKey))
                {
                    movie.onSubmitSkipButton();
                    Plugin.Logger.LogInfo("Skipping movie via key");
                    return;
                }

                if (Plugin.AutoSkipMovies.Value > 0f)
                {
                    if (_movieSeenAt < 0f)
                    {
                        _movieSeenAt = Time.unscaledTime;
                        _autoSkipped = false;
                    }
                    else if (!_autoSkipped &&
                        Time.unscaledTime - _movieSeenAt >= Plugin.AutoSkipMovies.Value)
                    {
                        movie.onSubmitSkipButton();
                        _autoSkipped = true;
                        Plugin.Logger.LogInfo("Skipping movie via auto timer");
                    }
                }
            }

            if (!active)
            {
                _movieSeenAt = -1f;
                _autoSkipped = false;
            }
        }

        private static void ApplyVolumeToggles()
        {
            var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<Volume>());
            for (int i = 0; i < objects.Length; i++)
            {
                var volume = objects[i] as Volume;
                if (volume == null || volume.profile == null) continue;

                var components = volume.profile.components;
                if (components == null) continue;

                for (int j = 0; j < components.Count; j++)
                {
                    var component = components[j];
                    if (component == null) continue;

                    string name = component.GetType().Name;
                    bool disable =
                        (Plugin.DisableMotionBlur.Value && name == "MotionBlur") ||
                        (Plugin.DisableDepthOfField.Value && name == "DepthOfField") ||
                        (Plugin.DisableBloom.Value && name == "Bloom") ||
                        (Plugin.DisableVignette.Value && name == "Vignette") ||
                        (Plugin.DisableFilmGrain.Value && name == "FilmGrain") ||
                        (Plugin.DisableChromaticAberration.Value && name == "ChromaticAberration") ||
                        (Plugin.DisableLensDistortion.Value && name == "LensDistortion");

                    if (disable && component.active)
                        component.active = false;
                }
            }
        }

        private static void ApplyFov()
        {
            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (cam == null || cam.orthographic) continue;
                if (Mathf.Abs(cam.fieldOfView - Plugin.FieldOfView.Value) > 0.01f)
                    cam.fieldOfView = Plugin.FieldOfView.Value;
            }
        }
    }

    // Skip the entry/publisher logo sequence. EntryScene.LogoManage is a private
    // nested type, so the target is resolved through AccessTools.
    [HarmonyPatch]
    internal static class LogoSkipBeginPatch
    {
        private static MethodBase TargetMethod()
        {
            var type = AccessTools.Inner(typeof(EntryScene), "LogoManage");
            return type == null ? null : AccessTools.Method(type, "beginLogo");
        }

        private static bool Prefix()
        {
            return !Plugin.SkipLogos.Value;
        }
    }

    [HarmonyPatch]
    internal static class LogoSkipFinishPatch
    {
        private static MethodBase TargetMethod()
        {
            var type = AccessTools.Inner(typeof(EntryScene), "LogoManage");
            return type == null ? null : AccessTools.Method(type, "get_isFinishLogo");
        }

        private static void Postfix(ref bool __result)
        {
            if (Plugin.SkipLogos.Value) __result = true;
        }
    }

}
