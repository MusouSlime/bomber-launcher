using System.Diagnostics;

namespace BomberLauncher;

internal static class BgmConverter
{
    private static readonly string[] Extensions =
    {
        ".ogg", ".wav", ".mp3", ".flac", ".m4a", ".aac", ".opus", ".wma",
    };

    public static string CacheDir => Path.Combine(Core.BackupRoot, "bgm_cache");

    public static (string Dest, int Converted, int Skipped) Convert(
        string source)
    {
        source = Path.GetFullPath(source);
        string dest = Path.GetFullPath(CacheDir);
        if (string.Equals(source, dest,
                          StringComparison.OrdinalIgnoreCase))
        {
            int existing = Directory.Exists(dest)
                ? Directory.EnumerateFiles(dest, "*",
                      SearchOption.AllDirectories)
                    .Count(f => Extensions.Contains(
                        Path.GetExtension(f).ToLowerInvariant()))
                : 0;
            return (dest, existing, 0);
        }

        if (Directory.Exists(dest)) Directory.Delete(dest, true);
        Directory.CreateDirectory(dest);

        var files = Directory
            .EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(
                Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (files.Count == 0)
            throw new InvalidOperationException(
                "No audio files found in that folder.");

        string? ffmpeg = FindFfmpeg();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int converted = 0, skipped = 0;
        string? firstError = null;

        foreach (string src in files)
        {
            string ext = Path.GetExtension(src).ToLowerInvariant();
            string stem = SafeName(Path.GetFileNameWithoutExtension(src));
            if (ext is ".ogg" or ".wav")
            {
                string name = Unique(used, stem, ext);
                try
                {
                    File.Copy(src, Path.Combine(dest, name));
                    converted++;
                }
                catch (Exception ex)
                {
                    skipped++;
                    firstError ??= $"{Path.GetFileName(src)}: {ex.Message}";
                }
                continue;
            }
            if (ffmpeg == null)
            {
                skipped++;
                firstError ??= "ffmpeg.exe not found";
                continue;
            }
            string ogg = Unique(used, stem, ".ogg");
            if (RunFfmpeg(ffmpeg, src, Path.Combine(dest, ogg),
                          out string? error))
            {
                converted++;
            }
            else
            {
                skipped++;
                firstError ??= $"{Path.GetFileName(src)}: {error}";
            }
        }

        if (converted == 0)
            throw new InvalidOperationException(
                firstError ?? "No tracks could be converted.");
        return (dest, converted, skipped);
    }

    private static string Unique(HashSet<string> used, string stem,
                                 string ext)
    {
        string name = stem + ext;
        int n = 2;
        while (!used.Add(name))
            name = $"{stem}_{n++}{ext}";
        return name;
    }

    private static string SafeName(string stem)
    {
        var invalid = Path.GetInvalidFileNameChars();
        string cleaned = new string(stem
            .Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "track" : cleaned;
    }

    private static string? FindFfmpeg()
    {
        string local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(local)) return local;
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            string candidate = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static bool RunFfmpeg(string ffmpeg, string input,
                                  string output, out string? error)
    {
        error = null;
        try
        {
            var info = new ProcessStartInfo(ffmpeg)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (string arg in new[]
                     {
                         "-y", "-loglevel", "error", "-i", input, "-vn",
                         "-c:a", "libvorbis", "-q:a", "4", output,
                     })
                info.ArgumentList.Add(arg);
            using var process = Process.Start(info);
            if (process == null)
            {
                error = "could not start ffmpeg";
                return false;
            }
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode == 0) return true;
            string[] lines = stderr.Trim().Split('\n');
            error = lines.Length > 0 ? lines[^1].Trim() : "ffmpeg failed";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
