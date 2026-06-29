using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Lumos.Desktop;

/// App icon generation and Windows resource helpers.
public static class AppIcon
{
    /// The application identifier used for file associations and window class.
    public const string AppId = "LumosDesktop";

    /// The application display name.
    public const string AppName = "Lumos Desktop";

    /// The .lumos file extension (without dot).
    public const string FileExtension = "lumos";

    /// The content type identifier for .lumos files.
    public const string ContentType = "application/x-lumos-project";

    /// Get the path to the app data directory where the icon is cached.
    public static string IconCachePath =>
        Path.Combine(LumosSettings.GetAppDataDir(), "app_icon.ico");

    /// Ensure an .ico file exists at the cache path, generating one if needed.
    /// Returns the path to the .ico file, or null if generation fails.
    public static string? EnsureIconExists()
    {
        var icoPath = IconCachePath;
        if (File.Exists(icoPath))
            return icoPath;

        try
        {
            GeneratePlaceholderIcon(icoPath);
            return File.Exists(icoPath) ? icoPath : null;
        }
        catch
        {
            return null;
        }
    }

    /// Generate a minimal .ico file with a simple Lumos "L" logo.
    /// Uses a 32×32 icon with 4-bit color depth for compatibility.
    /// In production, replace this with a proper designer-created .ico file.
    private static void GeneratePlaceholderIcon(string outputPath)
    {
        // Create a simple 32×32 icon programmatically
        // ICO format: header + 1 directory entry + BMP data
        int size = 32;
        int bytesPerPixel = 4; // BGRA

        // Create pixel data: purple gradient circle on dark background
        var pixels = new byte[size * size * bytesPerPixel];
        int cx = size / 2, cy = size / 2, radius = size / 2 - 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = x - cx, dy = y - cy;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                int idx = (y * size + x) * bytesPerPixel;

                if (dist <= radius)
                {
                    // Inner circle: gradient from purple (#8B5CF6) to blue (#3B82F6)
                    double t = dist / radius;
                    byte r = (byte)(139 - (int)(t * (139 - 59)));
                    byte g = (byte)(92 - (int)(t * (92 - 130)));
                    byte b = (byte)(246 - (int)(t * (246 - 246)));
                    pixels[idx] = b;       // B
                    pixels[idx + 1] = g;   // G
                    pixels[idx + 2] = r;   // R
                    pixels[idx + 3] = 255; // A
                }
                else
                {
                    // Transparent outside circle
                    pixels[idx] = 0;
                    pixels[idx + 1] = 0;
                    pixels[idx + 2] = 0;
                    pixels[idx + 3] = 0;
                }
            }
        }

        // Write ICO file
        using var fs = File.Create(outputPath);
        using var bw = new BinaryWriter(fs);

        // ICO header: reserved(2), type=1(2), count=1(2)
        bw.Write((ushort)0);     // Reserved
        bw.Write((ushort)1);     // Type: ICO
        bw.Write((ushort)1);     // Count: 1 image

        // Directory entry: w, h, colors, reserved, planes, bpp, size, offset
        bw.Write((byte)size);    // Width
        bw.Write((byte)size);    // Height
        bw.Write((byte)0);       // Colors
        bw.Write((byte)0);       // Reserved
        bw.Write((ushort)1);     // Color planes
        bw.Write((ushort)32);    // Bits per pixel

        // BMP data size = header(40) + pixels(size*size*4)
        int bmpSize = 40 + pixels.Length;
        int headerSize = 6 + 16; // ICO header + 1 directory entry
        bw.Write(bmpSize);
        bw.Write(headerSize);    // Offset to BMP data

        // BMP info header (40 bytes)
        bw.Write(40);            // Header size
        bw.Write(size);          // Width
        bw.Write(size * 2);      // Height (doubled for ICO format)
        bw.Write((ushort)1);     // Planes
        bw.Write((ushort)32);    // BPP
        bw.Write(0);             // Compression (BI_RGB)
        bw.Write(pixels.Length); // Image size
        bw.Write(0);             // XPixelsPerMeter
        bw.Write(0);             // YPixelsPerMeter
        bw.Write(0);             // Colors used
        bw.Write(0);             // Important colors

        // Pixel data (BGRA) — ICO stores pixels bottom-up
        for (int y = size - 1; y >= 0; y--)
        {
            for (int x = 0; x < size; x++)
            {
                int idx = (y * size + x) * bytesPerPixel;
                bw.Write(pixels[idx]);      // B
                bw.Write(pixels[idx + 1]);  // G
                bw.Write(pixels[idx + 2]);  // R
                bw.Write(pixels[idx + 3]);  // A
            }
        }
    }

    /// Register the .lumos file association with the Windows registry.
    /// Requires admin privileges or per-user HKCU registration.
    /// Call during first-run setup or application install.
    public static void RegisterFileAssociation()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            using var classesRoot = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Classes", true);
            if (classesRoot == null) return;

            // Register file extension -> ProgID
            using (var extKey = classesRoot.CreateSubKey($".{FileExtension}"))
            {
                extKey.SetValue("", AppId);
                extKey.SetValue("Content Type", ContentType);
            }

            // Register ProgID -> command
            using (var progId = classesRoot.CreateSubKey(AppId))
            {
                progId.SetValue("", AppName);
                using (var defaultIcon = progId.CreateSubKey("DefaultIcon"))
                {
                    var iconPath = EnsureIconExists();
                    defaultIcon.SetValue("", iconPath ?? $"\"{exePath}\",0");
                }
                using (var shell = progId.CreateSubKey("shell"))
                using (var open = shell.CreateSubKey("open"))
                using (var command = open.CreateSubKey("command"))
                {
                    command.SetValue("", $"\"{exePath}\" \"%1\"");
                }
            }

            // Notify explorer
            NativeMethods.SHChangeNotify(
                NativeMethods.HChangeNotifyEventID.SHCNE_ASSOCCHANGED,
                NativeMethods.HChangeNotifyFlags.SHCNF_IDLIST,
                IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // File association registration is non-critical
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern void SHChangeNotify(
            HChangeNotifyEventID wEventId,
            HChangeNotifyFlags uFlags,
            IntPtr dwItem1,
            IntPtr dwItem2);

        public enum HChangeNotifyEventID : uint
        {
            SHCNE_ASSOCCHANGED = 0x08000000,
        }

        public enum HChangeNotifyFlags : uint
        {
            SHCNF_IDLIST = 0x0000,
        }
    }
}
