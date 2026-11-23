namespace BomberLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, a =>
                a.Equals("--ultra-low", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--ultralow", StringComparison.OrdinalIgnoreCase)))
        {
            string? game = Core.FindGame();
            if (game == null) { Environment.ExitCode = 1; return; }
            Core.SetPluginOption(game, "Performance", "UltraLow", true);
            Core.SetPluginOption(game, "Performance", "LowEnd", true);
            Core.SetPluginValue(game, "Performance", "TargetFps", "60");
            Environment.ExitCode = 0;
            return;
        }
        if (Array.Exists(args, a =>
                a.Equals("--low-end", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--lowend", StringComparison.OrdinalIgnoreCase)))
        {
            string? game = Core.FindGame();
            if (game == null) { Environment.ExitCode = 1; return; }
            Core.SetPluginOption(game, "Performance", "LowEnd", true);
            Core.SetPluginOption(game, "Performance", "UltraLow", false);
            Core.SetPluginValue(game, "Performance", "TargetFps", "60");
            Environment.ExitCode = 0;
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
