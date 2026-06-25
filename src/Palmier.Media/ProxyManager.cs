using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Palmier.Media;

public sealed class ProxyManager
{
    private readonly ConcurrentDictionary<string, string> _proxyPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _proxyDirectory;
    private bool _useProxies = true;

    public bool UseProxies
    {
        get => _useProxies;
        set => _useProxies = value;
    }

    public ProxyManager(string proxyDirectory)
    {
        _proxyDirectory = proxyDirectory;
        Directory.CreateDirectory(proxyDirectory);
    }

    public bool HasProxy(string originalPath)
    {
        return _proxyPaths.TryGetValue(originalPath, out var proxyPath) && File.Exists(proxyPath);
    }

    public string ResolvePath(string originalPath)
    {
        if (_useProxies && _proxyPaths.TryGetValue(originalPath, out var proxyPath) && File.Exists(proxyPath))
        {
            return proxyPath;
        }
        return originalPath;
    }

    public async Task<string> GenerateProxyAsync(
        string originalPath,
        int targetWidth = 1280,
        int targetHeight = 720,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (HasProxy(originalPath))
        {
            progress?.Report(1.0);
            return _proxyPaths[originalPath];
        }

        string proxyFileName = $"{Path.GetFileNameWithoutExtension(originalPath)}_proxy.mp4";
        string targetPath = Path.Combine(_proxyDirectory, proxyFileName);

        for (int i = 1; i <= 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
            progress?.Report(i / 10.0);
        }

        await File.WriteAllBytesAsync(targetPath, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, cancellationToken);

        _proxyPaths[originalPath] = targetPath;
        return targetPath;
    }

    public void RegisterProxy(string originalPath, string proxyPath)
    {
        _proxyPaths[originalPath] = proxyPath;
    }

    public void Clear()
    {
        _proxyPaths.Clear();
    }
}
