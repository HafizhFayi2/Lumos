using System;
using Avalonia;

namespace Lumos.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. Install crash reporter before any app code runs
        CrashReporter.Install();

        // 2. Load persistent settings
        var settings = LumosSettings.Load();
        App.Settings = settings;

        // 3. Register .lumos file association on first run
        if (settings.LastProjectPath == null)
        {
            AppIcon.RegisterFileAssociation();
        }

        // 4. Set app user model ID for Windows taskbar grouping
        try
        {
            _ = AppIcon.EnsureIconExists();
        }
        catch { /* non-critical */ }

        // 5. Start the Avalonia app
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        // 6. Save settings on exit
        App.Settings?.Save();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
