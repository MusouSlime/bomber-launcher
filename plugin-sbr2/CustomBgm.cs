using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using NVorbis;
using UnityEngine;

namespace SBR2QoL
{
    // Static BGM replacement that does not inject a managed MonoBehaviour.
    // Tracks are decoded from the launcher's OGG cache (or PCM WAV) with
    // managed code and uploaded through AudioClip.SetData. The static
    // SetData overload is required because the instance wrapper misreads
    // Il2CppStructArray length on this IL2CPP build.
    internal static class CustomBgmPlayer
    {
        internal static bool Bypass;

        private const int ChunkFrames = 1 << 15;

        private static AudioSource _source;
        private static GameObject _host;
        private static string _folder;
        private static string[] _files;
        private static int _playlistIndex;
        private static readonly System.Random Random = new System.Random(
            unchecked(Environment.TickCount * 397) ^ Guid.NewGuid().GetHashCode());
        private static SoundManager _owner;
        private static SoundManager _lastManager;
        private static string _label;
        private static bool _pendingChange;
        private static float _pendingFade;
        private static string _currentLabel;
        private static string _currentFile;
        private static readonly List<string> _bag = new List<string>();
        private static int _bagPos;
        private static bool _bagShuffle;
        private static int _bagCount;
        private static string _bagFolder;
        private static string _lastFile;
        private static float _startedAt;
        private static bool _paused;
        private static bool _selfTestScheduled;
        private static float _selfTestAt;
        private static float _selfTestCheckAt;

        internal static bool HasActive
        {
            get { return _source != null && _source.clip != null; }
        }

        internal static void NoteSoundManager(SoundManager manager)
        {
            if (manager != null) _lastManager = manager;
            if (_selfTestScheduled || !Plugin.SelfTest.Value || _lastManager == null)
                return;
            _selfTestScheduled = true;
            _selfTestAt = Time.realtimeSinceStartup + 5f;
        }

        private static bool EnsureSource()
        {
            if (_source != null) return true;
            try
            {
                _host = new GameObject("SBR2QoL_BGM");
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _host.hideFlags = HideFlags.HideAndDontSave;
                _source = _host.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = false;
                _source.spatialBlend = 0f;
                return _source != null;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Custom BGM source failed: " + e.Message);
                return false;
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

        private static bool IsBgmLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            return label.StartsWith("BGM_", StringComparison.OrdinalIgnoreCase) ||
                label.StartsWith("snd_bgm_", StringComparison.OrdinalIgnoreCase);
        }

        private static void RefreshFiles(string folder)
        {
            if (folder == _folder) return;
            _folder = folder;
            _playlistIndex = 0;
            _files = null;
            try
            {
                if (!Directory.Exists(folder)) return;
                var all = Directory.GetFiles(folder, "*",
                    SearchOption.AllDirectories);
                var playable = new List<string>();
                for (int i = 0; i < all.Length; i++)
                {
                    string ext = Path.GetExtension(all[i]).ToLowerInvariant();
                    if (ext == ".ogg" || ext == ".wav")
                        playable.Add(all[i]);
                }
                playable.Sort(StringComparer.OrdinalIgnoreCase);
                _files = playable.ToArray();
                _bag.Clear();
                _bagFolder = null;
                Plugin.Logger.LogInfo("Custom BGM: " + _files.Length +
                    " track(s) in " + folder);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Custom BGM folder: " + e.Message);
            }
        }

        private static string FileFor(string folder, string label)
        {
            RefreshFiles(folder);
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

        private static string RandomFile()
        {
            if (_files == null || _files.Length == 0) return null;
            return _files[Random.Next(_files.Length)];
        }

        private static string NextPlaylistFile()
        {
            if (_files == null || _files.Length == 0) return null;
            string file = _files[_playlistIndex % _files.Length];
            _playlistIndex = (_playlistIndex + 1) % _files.Length;
            return file;
        }

        internal static bool TryPlay(SoundManager owner, string label,
            float fade, bool change)
        {
            if (Bypass) return false;
            string folder = ResolveFolder(Plugin.CustomBgmFolder.Value);
            if (string.IsNullOrEmpty(folder)) return false;
            if (!EnsureSource()) return false;

            if (!change && label == _currentLabel &&
                _source.clip != null && _source.isPlaying)
                return true;

            string file = FileFor(folder, label);
            if (file == null)
            {
                if (!IsBgmLabel(label)) return false;
                file = Plugin.CustomBgmShuffle.Value ||
                       Plugin.CustomBgmPlaylist.Value
                    ? NextFromBag()
                    : null;
            }
            if (file == null) return false;

            _owner = owner;
            _label = label;
            _pendingChange = change;
            _pendingFade = fade;

            try
            {
                if (!PlayFile(file))
                {
                    Fallback();
                    return true;
                }
                _currentLabel = label;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogWarning("Custom BGM failed: " +
                    Path.GetFileName(file) + " (" + e.Message + ")");
                Fallback();
                return true;
            }
        }

        internal static AudioClip LoadClip(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".ogg") return LoadOgg(file);
            if (ext == ".wav") return LoadWav(file);
            Plugin.Logger.LogWarning("Custom BGM unsupported format: " + ext);
            return null;
        }

        private static AudioClip CreateClip(string file, int frames,
            int channels, int rate)
        {
            var clip = AudioClip.Create(Path.GetFileNameWithoutExtension(file),
                frames, channels, rate, false);
            if (clip == null || clip.channels != channels || clip.samples != frames)
            {
                Plugin.Logger.LogWarning("Custom BGM clip create failed: " +
                    "samples=" + (clip != null ? clip.samples : -1));
                return null;
            }
            return clip;
        }

        private static bool Upload(AudioClip clip, float[] data, int frames,
            int offsetFrames)
        {
            var native = new Il2CppInterop.Runtime.InteropTypes.Arrays
                .Il2CppStructArray<float>(data);
            return AudioClip.SetData(clip, native, frames, offsetFrames);
        }

        private static AudioClip LoadOgg(string file)
        {
            using (var reader = new VorbisReader(file))
            {
                int channels = reader.Channels;
                int rate = reader.SampleRate;
                long total = reader.TotalSamples;
                if (channels < 1 || rate < 1 || total < 1)
                {
                    Plugin.Logger.LogWarning("Custom BGM empty ogg: " +
                        Path.GetFileName(file));
                    return null;
                }
                int frames = (int)Math.Min(total, int.MaxValue / channels);
                AudioClip clip = CreateClip(file, frames, channels, rate);
                if (clip == null) return null;

                int done = 0;
                float[] buffer = new float[ChunkFrames * channels];
                while (done < frames)
                {
                    int want = Math.Min(ChunkFrames, frames - done);
                    int wantSamples = want * channels;
                    int filled = 0;
                    while (filled < wantSamples)
                    {
                        int n = reader.ReadSamples(buffer, filled,
                            wantSamples - filled);
                        if (n <= 0) break;
                        filled += n;
                    }
                    if (filled <= 0) break;
                    int gotFrames = filled / channels;
                    if (gotFrames <= 0) break;
                    if (!Upload(clip, buffer, gotFrames, done))
                    {
                        Plugin.Logger.LogWarning("Custom BGM upload failed at " +
                            done + "/" + frames);
                        return null;
                    }
                    done += gotFrames;
                }
                if (done < frames)
                {
                    Plugin.Logger.LogWarning("Custom BGM short ogg: " + done +
                        "/" + frames + " frames");
                }
                return clip;
            }
        }

        private static AudioClip LoadWav(string file)
        {
            byte[] b = File.ReadAllBytes(file);
            if (b.Length < 44 ||
                b[0] != (byte)'R' || b[1] != (byte)'I' ||
                b[2] != (byte)'F' || b[3] != (byte)'F' ||
                b[8] != (byte)'W' || b[9] != (byte)'A' ||
                b[10] != (byte)'V' || b[11] != (byte)'E')
                throw new InvalidDataException("not a RIFF/WAVE file");

            int fmt = 0, channels = 0, rate = 0, bits = 0;
            int dataPos = -1, dataLen = 0;
            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                int size = BitConverter.ToInt32(b, pos + 4);
                int chunk = pos + 8;
                if (size < 0 || chunk + size > b.Length)
                    size = b.Length - chunk;
                if (chunk + size > b.Length) size = b.Length - chunk;

                if (id == "fmt " && size >= 16)
                {
                    fmt = BitConverter.ToUInt16(b, chunk);
                    channels = BitConverter.ToUInt16(b, chunk + 2);
                    rate = BitConverter.ToInt32(b, chunk + 4);
                    bits = BitConverter.ToUInt16(b, chunk + 14);
                }
                else if (id == "data")
                {
                    dataPos = chunk;
                    dataLen = size;
                    break;
                }
                pos = chunk + size + (size & 1);
            }

            if (dataPos < 0 || channels < 1 || rate < 1 ||
                (bits != 8 && bits != 16 && bits != 24 && bits != 32))
                throw new InvalidDataException("unsupported WAV format");

            int bytesPerSample = bits / 8;
            int frameBytes = bytesPerSample * channels;
            if (frameBytes < 1) throw new InvalidDataException("empty WAV");
            int frames = dataLen / frameBytes;
            if (frames < 1) throw new InvalidDataException("empty WAV data");

            AudioClip clip = CreateClip(file, frames, channels, rate);
            if (clip == null) return null;

            int done = 0;
            while (done < frames)
            {
                int n = Math.Min(ChunkFrames, frames - done);
                float[] part = new float[n * channels];
                int p = dataPos + done * frameBytes;
                for (int i = 0; i < part.Length; i++)
                {
                    if (p + bytesPerSample > b.Length) break;
                    float v;
                    if (fmt == 3 && bits == 32)
                        v = BitConverter.ToSingle(b, p);
                    else if (bits == 16)
                        v = BitConverter.ToInt16(b, p) / 32768f;
                    else if (bits == 24)
                    {
                        int s = b[p] | (b[p + 1] << 8) | (b[p + 2] << 16);
                        if ((s & 0x800000) != 0) s |= unchecked((int)0xFF000000);
                        v = s / 8388608f;
                    }
                    else if (bits == 32)
                        v = BitConverter.ToInt32(b, p) / 2147483648f;
                    else
                        v = (b[p] - 128) / 128f;
                    part[i] = v;
                    p += bytesPerSample;
                }
                if (!Upload(clip, part, n, done))
                {
                    Plugin.Logger.LogWarning("Custom BGM upload failed at " +
                        done + "/" + frames);
                    return null;
                }
                done += n;
            }
            return clip;
        }

        private static void Fallback()
        {
            if (_source != null) _source.Stop();
            if (_owner == null) return;
            Bypass = true;
            try
            {
                if (_pendingChange)
                    _owner.changeBGM(_label, _pendingFade);
                else
                    _owner.playAsBGM(_label, _pendingFade);
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

        private static bool PlayFile(string file)
        {
            AudioClip clip = LoadClip(file);
            if (clip == null) return false;
            _source.Stop();
            _source.clip = clip;
            _source.volume = Mathf.Clamp01(Plugin.CustomBgmVolume.Value);
            _source.loop = false;
            _source.Play();
            _currentFile = file;
            _lastFile = file;
            _startedAt = Time.realtimeSinceStartup;
            if (!string.IsNullOrEmpty(_label)) _currentLabel = _label;
            Plugin.Logger.LogInfo("Custom BGM: " +
                (_label ?? "next") + " -> " +
                Path.GetFileName(file));
            return true;
        }

        private static void RefillBag(bool shuffle)
        {
            _bag.Clear();
            if (_files == null) return;
            _bag.AddRange(_files);
            if (shuffle && _bag.Count > 1)
            {
                for (int i = _bag.Count - 1; i > 0; i--)
                {
                    int j = Random.Next(i + 1);
                    string tmp = _bag[i];
                    _bag[i] = _bag[j];
                    _bag[j] = tmp;
                }
            }
            _bagShuffle = shuffle;
            _bagCount = _files.Length;
            _bagFolder = _folder;
            // Vary the starting track between launches; later refills start
            // at the top of the cycle so no track repeats early.
            _bagPos = string.IsNullOrEmpty(_lastFile) && _bag.Count > 1
                ? Random.Next(_bag.Count)
                : 0;
        }

        private static string NextFromBag()
        {
            if (_files == null || _files.Length == 0) return null;
            bool shuffle = Plugin.CustomBgmShuffle.Value;
            if (_bag.Count == 0 || _bagShuffle != shuffle ||
                _bagCount != _files.Length || _bagFolder != _folder)
                RefillBag(shuffle);
            if (_bagPos >= _bag.Count) RefillBag(shuffle);
            if (_bag.Count == 0) return null;

            int pick = _bagPos++;
            if (_files.Length > 1 && _bag[pick] == _lastFile)
            {
                for (int i = pick + 1; i < _bag.Count; i++)
                {
                    if (_bag[i] == _lastFile) continue;
                    string tmp = _bag[i];
                    _bag[i] = _bag[pick];
                    _bag[pick] = tmp;
                    break;
                }
            }
            return _bag[pick];
        }

        private static void PlayNext()
        {
            if (_files == null || _files.Length == 0) return;
            string file = NextFromBag();
            if (file == null) return;
            if (!PlayFile(file))
                Plugin.Logger.LogWarning("Custom BGM next failed: " +
                    Path.GetFileName(file));
        }

        internal static void Tick()
        {
            if (_source != null && _source.clip != null && !_paused &&
                _currentFile != null && _currentLabel != null &&
                Time.realtimeSinceStartup - _startedAt > 0.25f &&
                !_source.isPlaying)
            {
                PlayNext();
            }
            if (_source != null && _source.clip != null)
            {
                float volume = Mathf.Clamp01(Plugin.CustomBgmVolume.Value);
                if (Mathf.Abs(_source.volume - volume) > 0.001f)
                    _source.volume = volume;
            }
            if (_selfTestAt > 0f && Time.realtimeSinceStartup >= _selfTestAt)
            {
                _selfTestAt = 0f;
                Plugin.Logger.LogInfo("Custom BGM self-test: starting");
                TryPlay(_lastManager, "BGM_SelfTest", 0f, false);
                _selfTestCheckAt = Time.realtimeSinceStartup + 1.5f;
            }
            if (_selfTestCheckAt > 0f &&
                Time.realtimeSinceStartup >= _selfTestCheckAt)
            {
                _selfTestCheckAt = 0f;
                bool playing = _source != null && _source.isPlaying;
                float len = _source != null && _source.clip != null
                    ? _source.clip.length : 0f;
                float t = _source != null ? _source.time : 0f;
                Plugin.Logger.LogInfo("Custom BGM self-test: playing=" + playing +
                    " clipLength=" + len.ToString("0.00") + "s time=" +
                    t.ToString("0.00") + "s");
            }
        }

        internal static void Stop()
        {
            if (_source != null) _source.Stop();
            _paused = false;
            _currentLabel = null;
            _currentFile = null;
        }

        internal static void Pause()
        {
            _paused = true;
            if (_source != null) _source.Pause();
        }

        internal static void Unpause()
        {
            _paused = false;
            if (_source != null && _source.clip != null) _source.UnPause();
        }

        internal static void Restart()
        {
            if (_source == null || _source.clip == null) return;
            _source.Stop();
            _source.loop = false;
            _source.Play();
            _startedAt = Time.realtimeSinceStartup;
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
