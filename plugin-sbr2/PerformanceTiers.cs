using System;
using UnityEngine;

namespace SBR2QoL
{
    // SBR2 performance tiers. High-End is the ceiling and overrides the
    // low/ultra flags. When no tier is active nothing is touched, so the
    // game's own quality settings stay in control.
    internal static class PerformanceTiers
    {
        private static string _applied = "";
        private static float _nextCheck;

        internal static string Current()
        {
            if (Plugin.HighEnd.Value) return "high";
            if (Plugin.UltraLow.Value) return "ultra";
            if (Plugin.LowEnd.Value) return "low";
            return "off";
        }

        internal static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < _nextCheck) return;
            _nextCheck = now + 1f;
            string current = Current();
            if (current == "off" || current == _applied) return;
            Apply();
        }

        internal static void Apply()
        {
            string tier = Current();
            try
            {
                if (tier == "high")
                {
                    Resize(1f, 1f);
                    QualitySettings.masterTextureLimit = 0;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowDistance = 150f;
                    QualitySettings.pixelLightCount = 8;
                    QualitySettings.lodBias = 2f;
                    QualitySettings.antiAliasing = 8;
                    QualitySettings.vSyncCount = Plugin.HighEndVSync.Value ? 1 : 0;
                    Application.targetFrameRate = Plugin.HighEndFps.Value;
                }
                else if (tier == "ultra")
                {
                    float scale = Mathf.Clamp(Plugin.UltraRenderScale.Value,
                        0.25f, 1f);
                    Resize(scale, scale);
                    QualitySettings.masterTextureLimit = 2;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 0f;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.lodBias = 0.4f;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = Plugin.LowEndFps.Value;
                }
                else if (tier == "low")
                {
                    Resize(1f, 1f);
                    QualitySettings.masterTextureLimit = 1;
                    QualitySettings.shadows = ShadowQuality.Disable;
                    QualitySettings.shadowDistance = 0f;
                    QualitySettings.pixelLightCount = 0;
                    QualitySettings.lodBias = 0.7f;
                    QualitySettings.antiAliasing = 0;
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = Plugin.LowEndFps.Value;
                }
                else
                {
                    _applied = "off";
                    return;
                }
                _applied = tier;
                Plugin.Logger.LogInfo(
                    "Performance preset: " + tier +
                    " (fps=" + Application.targetFrameRate +
                    ", tex=" + QualitySettings.masterTextureLimit +
                    ", shadows=" + QualitySettings.shadows +
                    ", lights=" + QualitySettings.pixelLightCount + ")");
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Performance preset failed: " + e.Message);
            }
        }

        private static void Resize(float width, float height)
        {
            try
            {
                ScalableBufferManager.ResizeBuffers(width, height);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Render scale failed: " + e.Message);
            }
        }

        internal static void SetTier(string tier)
        {
            Plugin.LowEnd.Value = tier == "low";
            Plugin.UltraLow.Value = tier == "ultra";
            Plugin.HighEnd.Value = tier == "high";
            Apply();
        }
    }
}
