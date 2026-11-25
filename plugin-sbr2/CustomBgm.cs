using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace SBR2QoL
{
    public class CustomBgmPlayer : MonoBehaviour
    {
        internal static CustomBgmPlayer Instance;
        internal static bool Bypass;

        private AudioSource _source;
        private string _folder;
        private string[] _files;
        private int _playlistIndex;
        private readonly System.Random _random = new System.Random(
            unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());
        private Coroutine _loading;
        private SoundManager _owner;
        private string _label;
        private float _fade;
        private bool _change;

        private void Awake()
        {
            Instance = this;
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
        }

        internal static bool HasActive
        {
            get
            {
                return Instance != null && Instance._source != null &&
                    Instance._loading == null &&
                    Instance._source.clip != null;
            }
        }

        internal static bool HasCustom
        {
            get
            {
                return Instance != null && Instance._source != null &&
                    (Instance._loading != null || Instance._source.clip != null);
            }
        }

        private static string ResolveFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return null;
            if (!Path.IsPathRooted(folder))
                folder = Path.Combine(Paths.GameRootPath, folder);
            if (folder.StartsWith("/"))
                folder = "Z:" + folder.Replace('/', '\\');
            return folder;
        }

        private static string ToFileUri(string path)
        {
            string p = path.Replace('\\', '/');
            return new Uri("file:///" + p).AbsoluteUri;
        }

        private static bool IsBgmLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            return label.StartsWith("BGM_", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("snd_bgm_", StringComparison.OrdinalIgnoreCase);
        }

        private string FileFor(string label)
        {
            string folder = ResolveFolder(Plugin.CustomBgmFolder.Value);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;

            if (folder != _folder)
            {
                _folder = folder;
                var all = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
                var playable = new List<string>();
                for (int i = 0; i < all.Length; i++)
                {
                    string ext = Path.GetExtension(all[i]).ToLowerInvariant();
                    if (ext == ".ogg" || ext == ".wav" || ext == ".mp3")
                        playable.Add(all[i]);
                }
                playable.Sort(StringComparer.OrdinalIgnoreCase);
                _files = playable.ToArray();
                _playlistIndex = 0;
                Plugin.Logger.LogInfo("Custom BGM: " + _files.Length +
                    " track(s) in " + folder);
            }

            if (_files == null || _files.Length == 0) return null;

            string want = Path.GetFileNameWithoutExtension(label);
            string baseWant = Regex.Replace(want, "_[a-z]$", "",
                RegexOptions.IgnoreCase);

            for (int i = 0; i < _files.Length; i++)
            {
                string stem = Path.GetFileNameWithoutExtension(_files[i]);
                if (string.Equals(stem, want, StringComparison.OrdinalIgnoreCase))
                    return _files[i];
            }
            for (int i = 0; i < _files.Length; i++)
            {
                string stem = Path.GetFileNameWithoutExtension(_files[i]);
                if (string.Equals(stem, baseWant, StringComparison.OrdinalIgnoreCase))
                    return _files[i];
            }
            return null;
        }

        private string NextPlaylistFile()
        {
            if (_files == null || _files.Length == 0) return null;
            string file = _files[_playlistIndex % _files.Length];
            _playlistIndex = (_playlistIndex + 1) % _files.Length;
            return file;
        }

        private string RandomFile()
        {
            if (_files == null || _files.Length == 0) return null;
            return _files[_random.Next(_files.Length)];
        }

        internal static bool TryPlay(SoundManager owner, string label,
            float fade, bool change)
        {
            if (Bypass || Instance == null) return false;
            if (string.IsNullOrEmpty(Plugin.CustomBgmFolder.Value)) return false;

            string file = Instance.FileFor(label);
            if (file == null)
            {
                if (!IsBgmLabel(label)) return false;
                file = Plugin.CustomBgmShuffle.Value
                    ? Instance.RandomFile()
                    : Plugin.CustomBgmPlaylist.Value
                        ? Instance.NextPlaylistFile()
                        : null;
            }
            if (file == null) return false;

            Instance._owner = owner;
            Instance._label = label;
            Instance._fade = fade;
            Instance._change = change;
            if (Instance._loading != null)
                Instance.StopCoroutine(Instance._loading);
            Instance._loading = Instance.StartCoroutine(
                Instance.LoadAndPlay(file));
            return true;
        }

        private IEnumerator LoadAndPlay(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            AudioType audioType = ext == ".wav" ? AudioType.WAV :
                (ext == ".mp3" ? AudioType.MPEG : AudioType.OGGVORBIS);
            var uri = new Il2CppSystem.Uri(ToFileUri(file));
            var request = UnityWebRequestMultimedia.GetAudioClip(uri, audioType);
            yield return request.SendWebRequest();

            if (!string.IsNullOrEmpty(request.error))
            {
                Plugin.Logger.LogWarning("Custom BGM failed: " + file +
                    " (" + request.error + ")");
                request.Dispose();
                _loading = null;
                Fallback();
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            request.Dispose();

            if (clip == null)
            {
                Plugin.Logger.LogWarning("Custom BGM decode failed: " + file +
                    " (use .ogg or .wav)");
                _loading = null;
                Fallback();
                yield break;
            }

            _source.clip = clip;
            _source.volume = Mathf.Clamp01(Plugin.CustomBgmVolume.Value);
            _source.loop = true;
            _source.Play();
            Plugin.Logger.LogInfo("Custom BGM: " + _label + " -> " +
                Path.GetFileName(file));
            _loading = null;
        }

        private void Fallback()
        {
            if (_source != null) _source.Stop();
            if (_owner == null) return;
            Bypass = true;
            try
            {
                if (_change) _owner.changeBGM(_label, _fade);
                else _owner.playAsBGM(_label, _fade);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("BGM fallback failed: " + e.Message);
            }
            finally
            {
                Bypass = false;
            }
        }

        internal static void Stop()
        {
            if (Instance == null) return;
            if (Instance._loading != null)
            {
                Instance.StopCoroutine(Instance._loading);
                Instance._loading = null;
            }
            if (Instance._source == null) return;
            Instance._source.Stop();
            Instance._source.clip = null;
        }

        internal static void Pause()
        {
            if (Instance != null && Instance._source != null)
                Instance._source.Pause();
        }

        internal static void Unpause()
        {
            if (Instance != null && Instance._source != null)
                Instance._source.Play();
        }

        internal static void Restart()
        {
            if (Instance != null && Instance._source != null &&
                Instance._source.clip != null)
            {
                Instance._source.Stop();
                Instance._source.Play();
            }
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playAsBGM",
        new Type[] { typeof(string), typeof(float) })]
    internal static class BgmPlayPatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            float fadeTime)
        {
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, false);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playAsBGM",
        new Type[] { typeof(string), typeof(bool), typeof(float) })]
    internal static class BgmPlayPushPatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            bool forcePush, float fadeTime)
        {
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, false);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playCrossFadeAsBGM",
        new Type[] { typeof(string), typeof(float) })]
    internal static class BgmCrossFadePatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            float fadeTime)
        {
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, false);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "playCrossFadeAsBGM",
        new Type[] { typeof(string), typeof(bool), typeof(float) })]
    internal static class BgmCrossFadePushPatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            bool forcePush, float fadeTime)
        {
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, false);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "changeBGM",
        new Type[] { typeof(string), typeof(float) })]
    internal static class BgmChangePatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            float fadeTime)
        {
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, true);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "changeCrossFadeBGM",
        new Type[] { typeof(string), typeof(float) })]
    internal static class BgmChangeCrossFadePatch
    {
        private static bool Prefix(SoundManager __instance, string bgmLabel,
            float fadeTime)
        {
            return !CustomBgmPlayer.TryPlay(__instance, bgmLabel, fadeTime, true);
        }
    }

    [HarmonyPatch(typeof(SoundManager), "stopBGM",
        new Type[] { typeof(float) })]
    internal static class BgmStopPatch
    {
        private static void Prefix()
        {
            CustomBgmPlayer.Stop();
        }
    }

    [HarmonyPatch(typeof(SoundManager), "onPauseBGM",
        new Type[] { typeof(float) })]
    internal static class BgmPausePatch
    {
        private static bool Prefix()
        {
            if (!CustomBgmPlayer.HasActive) return true;
            CustomBgmPlayer.Pause();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "offPauseBGM",
        new Type[] { typeof(float) })]
    internal static class BgmUnpausePatch
    {
        private static bool Prefix()
        {
            if (!CustomBgmPlayer.HasActive) return true;
            CustomBgmPlayer.Unpause();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "restartBGM",
        new Type[] { typeof(float) })]
    internal static class BgmRestartPatch
    {
        private static bool Prefix()
        {
            if (!CustomBgmPlayer.HasActive) return true;
            CustomBgmPlayer.Restart();
            return false;
        }
    }

    [HarmonyPatch(typeof(SoundManager), "popBGM",
        new Type[] { typeof(bool), typeof(bool), typeof(float) })]
    internal static class BgmPopPatch
    {
        private static void Prefix()
        {
            CustomBgmPlayer.Stop();
        }
    }

    [HarmonyPatch(typeof(SoundManager), "popCrossFadeBGM",
        new Type[] { typeof(bool), typeof(float) })]
    internal static class BgmPopCrossFadePatch
    {
        private static void Prefix()
        {
            CustomBgmPlayer.Stop();
        }
    }
}
