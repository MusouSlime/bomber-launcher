namespace BomberLauncher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (Array.Exists(args, a =>
                a.Equals("--low-end", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("--lowend", StringComparison.OrdinalIgnoreCase)))
        {
            string? game = Core.FindGame();
            if (game == null) { Environment.ExitCode = 1; return; }
            Core.SetPluginOption(game, "Performance", "LowEnd", true);
            Core.SetPluginValue(game, "Performance", "TargetFps", "30");
            Environment.ExitCode = 0;
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
