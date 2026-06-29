using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Lumos.Desktop;

/// Sets up global exception handlers and writes crash logs to
/// %APPDATA%/LumosDesktop/crashes/.
public static class CrashReporter
{
    private static readonly string CrashDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LumosDesktop", "crashes");

    private static bool _initialized;

    /// Install global exception handlers. Call once at startup before
    /// any application code runs.
    public static void Install()
    {
        if (_initialized) return;
        _initialized = true;

        Directory.CreateDirectory(CrashDir);

        // Unhandled exceptions on any thread
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // First-chance exceptions — log in debug builds only
#if DEBUG
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
#endif

        // Task scheduler unobserved exceptions
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// Write a crash report to disk.
    public static string WriteCrashReport(string title, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(CrashDir);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            var filename = $"crash_{timestamp}_{Process.GetCurrentProcess().Id}.log";
            var path = Path.Combine(CrashDir, filename);

            var report = new[]
            {
                $"=== Lumos Desktop Crash Report ===",
                $"Timestamp: {DateTime.UtcNow:O}",
                $"Title: {title}",
                $"OS: {Environment.OSVersion}",
                $"Process: {Process.GetCurrentProcess().ProcessName}",
                $"PID: {Process.GetCurrentProcess().Id}",
                $"CLR: {Environment.Version}",
                $"Working Directory: {Environment.CurrentDirectory}",
                $"Command Line: {Environment.CommandLine}",
                $"",
                $"Exception Type: {ex.GetType().FullName}",
                $"Message: {ex.Message}",
                $"Stack Trace:",
                ex.ToString(),
                $"",
                $"=== End of Crash Report ===",
            };

            File.WriteAllLines(path, report);
            return path;
        }
        catch
        {
            return null!;
        }
    }

    /// Get list of recent crash logs.
    public static IReadOnlyList<string> GetRecentCrashLogs(int count = 5)
    {
        try
        {
            if (!Directory.Exists(CrashDir))
                return Array.Empty<string>();

            return Directory.GetFiles(CrashDir, "crash_*.log")
                .OrderByDescending(f => f)
                .Take(count)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        var title = e.IsTerminating ? "Fatal unhandled exception" : "Non-terminating unhandled exception";
        WriteCrashReport(title, ex ?? new Exception("Unknown unhandled exception"));
    }

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        // Only log exceptions that are likely to be interesting
        if (e.Exception is NullReferenceException or InvalidOperationException
            or ArgumentNullException or KeyNotFoundException)
        {
            Debug.WriteLine($"[FirstChance] {e.Exception.GetType().Name}: {e.Exception.Message}");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var ex = e.Exception?.InnerException ?? e.Exception;
        if (ex != null)
        {
            WriteCrashReport("Unobserved task exception", ex);
        }
    }

}
