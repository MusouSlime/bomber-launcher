using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BomberLauncher;

internal static class Core
{
    public const string AppId = "702700";
    public static readonly string BackupRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BomberLauncher");

    private static readonly byte[] PatE = { 0x05, 0x5f, 0x00, 0x45, 0x00, 0x00 };
    private static readonly byte[] PatJ = { 0x05, 0x5f, 0x00, 0x4a, 0x00, 0x00 };

    public static string? FindGame()
    {
        foreach (string lib in SteamLibraries())
        {
            string p = Path.Combine(lib, "steamapps", "common",
                                    "Super Bomberman R");
            if (File.Exists(Path.Combine(p, "SuperBombermanR.exe")))
                return p;
        }
        return null;
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var roots = new List<string>();
        try
        {
            string? path = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)
                as string;
            if (!string.IsNullOrEmpty(path)) roots.Add(path.Replace('/', '\\'));
        }
        catch { }
        roots.Add(@"C:\Program Files (x86)\Steam");
        roots.Add(@"C:\Program Files\Steam");

        foreach (string root in roots.Distinct())
        {
            if (Directory.Exists(root)) yield return root;
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf),
                     "\"path\"\\s+\"([^\"]+)\""))
            {
                string lib = m.Groups[1].Value.Replace(@"\\", @"\");
                if (Directory.Exists(lib)) yield return lib;
            }
        }
    }

    public static bool GameRunning() =>
        Process.GetProcessesByName("SuperBombermanR").Length > 0;

    private static string DllPath(string game) => Path.Combine(
        game, "SuperBombermanR_Data", "Managed", "Assembly-CSharp.dll");

    private static string BinDir(string game) => Path.Combine(
        game, "SuperBombermanR_Data", "StreamingAssets", "bin");

    private static string SavePath(string game) =>
        Path.Combine(game, "savedata.bin");

    private static string PluginPath(string game) => Path.Combine(
        game, "BepInEx", "plugins", "BomberQoL.dll");

    private static string PluginCfg(string game) => Path.Combine(
        game, "BepInEx", "config", "bomber.qol.cfg");

    public static bool PluginInstalled(string game) =>
        File.Exists(PluginPath(game));

    private static int Count(byte[] hay, byte[] needle)
    {
        int n = 0;
        for (int i = 0; i <= hay.Length - needle.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { ok = false; break; }
            if (ok) n++;
        }
        return n;
    }

    public static bool DualAudio(string game)
    {
        byte[] d = File.ReadAllBytes(DllPath(game));
        if (Count(d, PatE) == 1 && Count(d, PatJ) == 1) return false;
        if (Count(d, PatE) == 0 && Count(d, PatJ) == 2) return true;
        throw new InvalidOperationException("Unrecognized Assembly-CSharp.dll");
    }

    public static void SetDualAudio(string game, bool enabled)
    {
        if (GameRunning()) throw new InvalidOperationException("Close the game first.");
        string dll = DllPath(game);
        byte[] d = File.ReadAllBytes(dll);
        string backup = Path.Combine(BackupRoot, "backup", "Assembly-CSharp.dll.orig");
        if (enabled)
        {
            int idx = IndexOf(d, PatE);
            if (idx < 0)
            {
                if (Count(d, PatJ) == 2) return;
                throw new InvalidOperationException("Patch pattern not found.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
            if (!File.Exists(backup)) File.Copy(dll, backup);
            d[idx + 3] = 0x4A;
            File.WriteAllBytes(dll, d);
        }
        else
        {
            if (File.Exists(backup))
            {
                File.Copy(backup, dll, true);
                return;
            }
            if (Count(d, PatJ) == 2)
            {
                int idx = IndexOf(d, PatJ);
                d[idx + 3] = 0x45;
                File.WriteAllBytes(dll, d);
                return;
            }
            throw new InvalidOperationException(
                "No backup found. Verify game files in Steam.");
        }
    }

    private static int IndexOf(byte[] hay, byte[] needle)
    {
        for (int i = 0; i <= hay.Length - needle.Length; i++)
        {
            bool ok = true;
            for (int j = 0; j < needle.Length; j++)
                if (hay[i + j] != needle[j]) { ok = false; break; }
            if (ok) return i;
        }
        return -1;
    }

    public static bool? IntroSkipped(string game)
    {
        string[] files = { "bom_00_intro_a", "bom_00_intro_b" };
        var sizes = files.Select(f =>
        {
            string p = Path.Combine(BinDir(game), f);
            return File.Exists(p) ? new FileInfo(p).Length : -1;
        }).ToArray();
        if (sizes.Any(s => s < 0)) return null;
        if (sizes.All(s => s < 1024 * 1024)) return true;
        if (sizes.All(s => s > 10L * 1024 * 1024)) return false;
        return null;
    }

    public static void SetIntroSkip(string game, bool enabled)
    {
        if (GameRunning()) throw new InvalidOperationException("Close the game first.");
        string bin = BinDir(game);
        string backupDir = Path.Combine(BackupRoot, "backup", "videos");
        string[] files = { "bom_00_intro_a", "bom_00_intro_b" };
        if (enabled)
        {
            Directory.CreateDirectory(backupDir);
            byte[] blank = ReadResource("intro_blank.mp4");
            foreach (string f in files)
            {
                string p = Path.Combine(bin, f);
                string b = Path.Combine(backupDir, f);
                if (File.Exists(p) && new FileInfo(p).Length > 10L * 1024 * 1024
                    && !File.Exists(b))
                    File.Move(p, b);
                File.WriteAllBytes(p, blank);
            }
        }
        else
        {
            int restored = 0;
            foreach (string f in files)
            {
                string p = Path.Combine(bin, f);
                string b = Path.Combine(backupDir, f);
                if (File.Exists(b)) { File.Copy(b, p, true); restored++; }
            }
            if (restored != 2)
                throw new InvalidOperationException(
                    "Original movies missing. Use Steam > Verify files.");
        }
    }

    private static byte[] ReadResource(string name)
    {
        using Stream? s = typeof(Core).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing embedded " + name);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    // ---- save language --------------------------------------------------
    private static byte[] Crypt(byte[] data)
    {
        uint s = 1962962729;
        byte[] o = (byte[])data.Clone();
        for (int i = 0; i < o.Length; i++)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            o[i] ^= (byte)(s & 0xFF);
        }
        return o;
    }

    private static List<KeyValuePair<string, byte[]>> ParseSave(byte[] data)
    {
        int pos = 0;
        var list = new List<KeyValuePair<string, byte[]>>();
        while (pos < data.Length)
        {
            int len = 0, shift = 0;
            while (true)
            {
                byte b = data[pos++];
                len |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) break;
                shift += 7;
            }
            string key = Encoding.UTF8.GetString(data, pos, len);
            pos += len;
            int size = BitConverter.ToInt32(data, pos); pos += 4;
            byte[] blob = data[pos..(pos + size)]; pos += size;
            int hsize = BitConverter.ToInt32(data, pos); pos += 4;
            pos += hsize;
            list.Add(new(key, blob));
        }
        return list;
    }

    private static byte[] BuildSave(List<KeyValuePair<string, byte[]>> entries)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, true);
        foreach (var e in entries)
        {
            w.Write(e.Key);
            w.Write(e.Value.Length);
            w.Write(e.Value);
            byte[] h = MD5.HashData(e.Value);
            w.Write(h.Length);
            w.Write(h);
        }
        return Crypt(ms.ToArray());
    }

    public static byte ReadLanguage(string game)
    {
        var entries = ParseSave(Crypt(File.ReadAllBytes(SavePath(game))));
        var sys = entries.FirstOrDefault(e => e.Key == "/systemData");
        if (sys.Value is null || sys.Value.Length != 1352)
            throw new InvalidOperationException("Unsupported save layout.");
        return sys.Value[1132];
    }

    public static void SetLanguage(string game, byte lang)
    {
        if (GameRunning()) throw new InvalidOperationException("Close the game first.");
        string save = SavePath(game);
        byte[] raw = File.ReadAllBytes(save);
        var entries = ParseSave(Crypt(raw));
        int idx = entries.FindIndex(e => e.Key == "/systemData");
        if (idx < 0 || entries[idx].Value.Length != 1352)
            throw new InvalidOperationException("Unsupported save layout.");
        string dir = Path.Combine(BackupRoot, "backup", "saves");
        Directory.CreateDirectory(dir);
        File.Copy(save, Path.Combine(dir,
            $"savedata_{DateTime.Now:yyyyMMdd_HHmmss}.bin"), true);
        byte[] blob = (byte[])entries[idx].Value.Clone();
        blob[1132] = lang;
        entries[idx] = new("/systemData", blob);
        File.WriteAllBytes(save, BuildSave(entries));
    }

    // ---- BepInEx config -------------------------------------------------
    public static string GetPluginOption(string game, string section,
                                         string key, string fallback)
    {
        string path = PluginCfg(game);
        if (!File.Exists(path)) return fallback;
        bool inSec = false;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSec = line[1..^1].Trim() == section;
                continue;
            }
            if (inSec && line.Contains('='))
            {
                int eq = line.IndexOf('=');
                if (line[..eq].Trim() == key)
                    return line[(eq + 1)..].Trim();
            }
        }
        return fallback;
    }

    public static void SetPluginOption(string game, string section,
                                       string key, bool value)
    {
        string path = PluginCfg(game);
        var lines = File.Exists(path)
            ? new List<string>(File.ReadAllLines(path))
            : new List<string>();
        bool inSec = false, foundSec = false, written = false;
        for (int i = 0; i < lines.Count; i++)
        {
            string s = lines[i].Trim();
            if (s.StartsWith('[') && s.EndsWith(']'))
            {
                inSec = s[1..^1].Trim() == section;
                foundSec |= inSec;
            }
            else if (inSec && s.Contains('=') &&
                     s[..s.IndexOf('=')].Trim() == key)
            {
                lines[i] = $"{key} = {(value ? "true" : "false")}";
                written = true;
            }
        }
        if (!foundSec)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
            lines.Add($"[{section}]");
            lines.Add($"{key} = {(value ? "true" : "false")}");
        }
        else if (!written)
        {
            int at = lines.FindIndex(l =>
                l.Trim().StartsWith('[') && l.Trim()[1..^1].Trim() == section);
            lines.Insert(at + 1, $"{key} = {(value ? "true" : "false")}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }

    // ---- actions --------------------------------------------------------
    public static void BackupSave(string game)
    {
        string dir = Path.Combine(BackupRoot, "backup", "saves");
        Directory.CreateDirectory(dir);
        File.Copy(SavePath(game), Path.Combine(dir,
            $"savedata_{DateTime.Now:yyyyMMdd_HHmmss}.bin"), true);
    }

    public static void RestoreAll(string game)
    {
        if (GameRunning()) throw new InvalidOperationException("Close the game first.");
        string backup = Path.Combine(BackupRoot, "backup",
                                     "Assembly-CSharp.dll.orig");
        if (File.Exists(backup)) File.Copy(backup, DllPath(game), true);
        foreach (string f in new[] { "bom_00_intro_a", "bom_00_intro_b" })
        {
            string b = Path.Combine(BackupRoot, "backup", "videos", f);
            if (File.Exists(b))
                File.Copy(b, Path.Combine(BinDir(game), f), true);
        }
    }

    public static void Launch(string game, bool borderless)
    {
        string? steamExe = null;
        try
        {
            steamExe = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamExe", null)
                as string;
        }
        catch { }
        if (string.IsNullOrEmpty(steamExe) || !File.Exists(steamExe))
            steamExe = @"C:\Program Files (x86)\Steam\steam.exe";
        string args = "-applaunch " + AppId;
        if (borderless)
        {
            var b = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            args += $" -popupwindow -screen-width {b.Width} -screen-height {b.Height}";
        }
        Process.Start(new ProcessStartInfo(steamExe, args)
        {
            UseShellExecute = true
        });
    }
}
