using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace SBR2QoL
{
    // Replaces bomb/explosion sound labels with a user-supplied folder of
    // classic Bomberman explosion clips. Only labels containing one of the
    // configured fragments are intercepted; everything else plays normally.
    internal static class ExplosionSfx
    {
        private static GameObject _host;
        private static readonly List<AudioSource> Sources = new List<AudioSource>();
        private static int _nextSource;
        private static string _folder;
        private static string[] _files;
        private static int _sequence;
        private static string _lastFile;
        private static readonly Dictionary<string, AudioClip> Clips =
            new Dictionary<string, AudioClip>();
        private static readonly System.Random Random = new System.Random(
            unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());
        internal const int CustomIdBase = 1000000;
        private static int _nextId = CustomIdBase;

        internal static int NextId()
        {
            return _nextId++;
        }

        private static bool Enabled
        {
            get { return !string.IsNullOrEmpty(Plugin.ExplosionSfxFolder.Value); }
        }

        private static void Ensure()
        {
            if (_host != null) return;
            _host = new GameObject("SBR2QoL_ExplosionSfx");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.hideFlags = HideFlags.HideAndDontSave;
            for (int i = 0; i < 4; i++)
            {
                var source = _host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                Sources.Add(source);
            }
        }

        private static void Refresh()
        {
            string folder = Plugin.ExplosionSfxFolder.Value;
            if (string.IsNullOrEmpty(folder))
            {
                _folder = null;
                _files = null;
                return;
            }
            if (!Path.IsPathRooted(folder))
                folder = Path.Combine(Paths.GameRootPath, folder);
            if (folder.StartsWith("/"))
                folder = "Z:" + folder.Replace('/', '\\');
            if (folder == _folder && _files != null) return;

            _folder = folder;
            _files = null;
            Clips.Clear();
            try
            {
                if (!Directory.Exists(folder)) return;
                var list = new List<string>();
                foreach (var file in Directory.GetFiles(folder, "*",
                    SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".ogg" || ext == ".wav")
                        list.Add(file);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
                _files = list.ToArray();
                Plugin.Logger.LogInfo("Explosion SFX: " + _files.Length +
                    " clip(s) in " + folder);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Explosion SFX folder: " + e.Message);
            }
        }

        private static bool IsExplosion(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            string filters = Plugin.ExplosionSfxLabels.Value ?? "";
            foreach (string part in filters.Split(','))
            {
                string fragment = part.Trim();
                if (fragment.Length > 0 &&
                    label.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        internal static bool TryPlay(string label, float pan = 0f)
        {
            if (!Enabled || !IsExplosion(label)) return false;
            Refresh();
            if (_files == null || _files.Length == 0) return false;
            Ensure();

            string file = PickFile();
            if (file == null) return false;

            AudioClip clip;
            if (!Clips.TryGetValue(file, out clip))
            {
                clip = CustomBgmPlayer.LoadClip(file);
                if (clip == null) return false;
                Clips[file] = clip;
            }

            var source = Sources[_nextSource % Sources.Count];
            _nextSource++;
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(Plugin.ExplosionSfxVolume.Value);
            source.panStereo = Mathf.Clamp(pan, -1f, 1f);
            source.Play();
            _lastFile = file;
            Plugin.Logger.LogInfo("Explosion SFX: " + label + " -> " +
                Path.GetFileName(file));
            return true;
        }

        internal static void PlayTest()
        {
            if (!TryPlay("SE_bom_test")) 
                Plugin.Logger.LogInfo(
                    "Explosion SFX test: no clips configured or label mismatch");
        }

        private static string PickFile()
        {
            if (_files == null || _files.Length == 0) return null;
            if (_files.Length == 1) return _files[0];

            if (Plugin.ExplosionSfxShuffle.Value)
            {
                for (int i = 0; i < 8; i++)
                {
                    string candidate = _files[Random.Next(_files.Length)];
                    if (candidate != _lastFile) return candidate;
                }
                return _files[Random.Next(_files.Length)];
            }

            for (int i = 0; i < _files.Length; i++)
            {
                _sequence = (_sequence + 1) % _files.Length;
                if (_files[_sequence] != _lastFile)
                    return _files[_sequence];
            }
            return _files[0];
        }
    }

    [HarmonyPatch(typeof(SoundManager), "play",
        new[] { typeof(string), typeof(float) })]
    internal static class ExplosionPlayPatch
    {
        private static bool Prefix(string soundLabel, ref int __result)
        {
            if (!ExplosionSfx.TryPlay(soundLabel)) return true;
            __result = ExplosionSfx.NextId();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "isPlaying",
        new[] { typeof(int) })]
    internal static class ExplosionIsPlayingPatch
    {
        private static bool Prefix(int instanceId, ref bool __result)
        {
            if (instanceId < ExplosionSfx.CustomIdBase) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "setSePosition",
        new[] { typeof(int), typeof(Vector3) })]
    internal static class ExplosionPositionPatch
    {
        private static bool Prefix(int instanceId)
        {
            return instanceId < ExplosionSfx.CustomIdBase;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "pause",
        new[] { typeof(int), typeof(float) })]
    internal static class ExplosionPausePatch
    {
        private static bool Prefix(int instanceId)
        {
            return instanceId < ExplosionSfx.CustomIdBase;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playWithPan",
        new[] { typeof(string), typeof(float), typeof(float) })]
    internal static class ExplosionPlayWithPanPatch
    {
        private static bool Prefix(string soundLabel, float pan,
            ref int __result)
        {
            if (!ExplosionSfx.TryPlay(soundLabel, pan)) return true;
            __result = ExplosionSfx.NextId();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playFade",
        new[] { typeof(string), typeof(float), typeof(float) })]
    internal static class ExplosionPlayFadePatch
    {
        private static bool Prefix(string soundLabel, ref int __result)
        {
            if (!ExplosionSfx.TryPlay(soundLabel)) return true;
            __result = ExplosionSfx.NextId();
            return false;
        }
    }
}
