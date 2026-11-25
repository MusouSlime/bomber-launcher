using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SBR2QoL
{
    // Harmony-only QoL runtime. Everything here runs from patches on existing
    // game methods; no managed MonoBehaviour is injected.
    internal static class QoLStatic
    {
        private static float _nextPostFx;
        private static float _nextFov;
        private static Camera _camera;
        private static int _lastFrame = -1;

        internal static void Init()
        {
        }

        internal static void Tick()
        {
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;
            SettingsOverlay.Tick();
            CustomBgmPlayer.Tick();
            PerformanceTiers.Tick();

            float now = Time.unscaledTime;
            if (now >= _nextPostFx)
            {
                _nextPostFx = now + 0.5f;
                ApplyPostFx();
            }
            if (Plugin.FieldOfView.Value > 0f && now >= _nextFov)
            {
                _nextFov = now + 0.5f;
                ApplyFov();
            }
        }

        private static void ApplyPostFx()
        {
            try
            {
                var manager = VolumeManager.instance;
                if (manager == null) return;
                var stack = manager.stack;
                if (stack == null) return;

                SetPostFx<MotionBlur>(stack, Plugin.DisableMotionBlur.Value);
                SetPostFx<DepthOfField>(stack, Plugin.DisableDepthOfField.Value);
                SetPostFx<Bloom>(stack, Plugin.DisableBloom.Value);
                SetPostFx<Vignette>(stack, Plugin.DisableVignette.Value);
                SetPostFx<FilmGrain>(stack, Plugin.DisableFilmGrain.Value);
                SetPostFx<ChromaticAberration>(stack,
                    Plugin.DisableChromaticAberration.Value);
                SetPostFx<LensDistortion>(stack,
                    Plugin.DisableLensDistortion.Value);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("QoL post-fx: " + e.Message);
            }
        }

        private static void SetPostFx<T>(VolumeStack stack, bool disable)
            where T : VolumeComponent
        {
            if (!disable) return;
            var component = stack.GetComponent<T>();
            if (component != null && component.active)
                component.active = false;
        }

        private static void ApplyFov()
        {
            try
            {
                if (_camera == null)
                    _camera = Camera.main;
                if (_camera == null || _camera.orthographic) return;
                if (Mathf.Abs(_camera.fieldOfView - Plugin.FieldOfView.Value) > 0.01f)
                    _camera.fieldOfView = Plugin.FieldOfView.Value;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("QoL fov: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(SoundManager), "Update")]
    internal static class QoLSoundUpdatePatch
    {
        private static bool _logged;
        private static float _nextBeat;

        private static void Postfix(SoundManager __instance)
        {
            if (!_logged)
            {
                _logged = true;
                Plugin.Logger.LogInfo("SBR2 QoL tick active (SoundManager.Update)");
            }
            if (Plugin.SelfTest.Value &&
                Time.realtimeSinceStartup >= _nextBeat)
            {
                _nextBeat = Time.realtimeSinceStartup + 5f;
                Plugin.Logger.LogInfo("SBR2 QoL heartbeat t=" +
                    Time.realtimeSinceStartup.ToString("0.0"));
            }
            CustomBgmPlayer.NoteSoundManager(__instance);
            QoLStatic.Tick();
        }
    }

    [HarmonyPatch(typeof(SoundManager), "play",
        new[] { typeof(string), typeof(float) })]
    internal static class SfxLabelLogPatch
    {
        private static readonly System.Collections.Generic.HashSet<string> Seen =
            new System.Collections.Generic.HashSet<string>();

        private static void Prefix(string soundLabel)
        {
            if (!Plugin.SelfTest.Value) return;
            if (soundLabel == null || Seen.Contains(soundLabel)) return;
            Seen.Add(soundLabel);
            Plugin.Logger.LogInfo("SFXPLAY " + soundLabel);
        }
    }

    [HarmonyPatch(typeof(MoviePlayUI), "update")]
    internal static class QoLMovieUpdatePatch
    {
        private static MoviePlayUI _seen;
        private static float _seenAt = -1f;
        private static bool _skipped;

        private static void Postfix(MoviePlayUI __instance)
        {
            if (__instance == null || !__instance.isActiveAndEnabled) return;
            if (!Plugin.SkipMovies.Value) return;

            if (Input.GetKeyDown(Plugin.SkipKey))
            {
                __instance.onSubmitSkipButton();
                Plugin.Logger.LogInfo("Skipping movie via key");
                return;
            }

            if (Plugin.AutoSkipMovies.Value <= 0f) return;
            if (_seen != __instance)
            {
                _seen = __instance;
                _seenAt = Time.unscaledTime;
                _skipped = false;
                return;
            }
            if (!_skipped &&
                Time.unscaledTime - _seenAt >= Plugin.AutoSkipMovies.Value)
            {
                __instance.onSubmitSkipButton();
                _skipped = true;
                Plugin.Logger.LogInfo("Skipping movie via auto timer");
            }
        }
    }
}
