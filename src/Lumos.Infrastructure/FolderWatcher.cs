using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lumos.Infrastructure;

public sealed class FolderWatcher : IDisposable
{
    private FileSystemWatcher? _watcher;

    public event EventHandler<string>? FileArrived;

    public void Watch(string path)
    {
        _watcher = new FileSystemWatcher(path)
        {
            NotifyFilter            = NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents     = true,
            IncludeSubdirectories   = false,
        };
        _watcher.Created += OnFileCreated;
        _watcher.Renamed += OnFileRenamed;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
        => _ = HandleFileAsync(e.FullPath);

    private void OnFileRenamed(object sender, RenamedEventArgs e)
        => _ = HandleFileAsync(e.FullPath);

    private async Task HandleFileAsync(string path)
    {
        // Wait until the file is fully written (retries with exponential backoff)
        if (!await WaitUntilUnlockedAsync(path)) return;
        FileArrived?.Invoke(this, path);
    }

    private static async Task<bool> WaitUntilUnlockedAsync(string path, int maxAttempts = 12)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                int delayMs = Math.Min(250 * (int)Math.Pow(2, attempt), 8000);
                await Task.Delay(delayMs);
            }
        }
        return false;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
