using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the safe-to-regenerate caches of Android Studio and JetBrains IDEs:
/// %LOCALAPPDATA%\Google\AndroidStudio*\caches, %APPDATA%\Google\AndroidStudio*\
/// caches and %APPDATA%\JetBrains\*\caches (plus the per-product "log" folders).
/// Settings, keymaps, plugins and local history are never touched.
/// </summary>
public sealed class AndroidStudioCacheScanner : IScanner
{
    public string Name => "Android Studio Caches";
    public ScannerType Type => ScannerType.AndroidStudioCache;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };

        return Task.Run(() =>
        {
            foreach (var productDir in DiscoverProductDirs())
                ScanProductInto(result, productDir);
            return result;
        });
    }

    /// <summary>Testable entry point: scan the cache/log subfolders of one product dir.</summary>
    internal Task<ScanResult> ScanDirectoryAsync(string productDir)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (!Directory.Exists(productDir))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            ScanProductInto(result, productDir);
            return result;
        });
    }

    private static void ScanProductInto(ScanResult result, string productDir)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cacheSub in new[] { "caches", "log", "Log" })
        {
            var target = Path.Combine(productDir, cacheSub);
            if (!Directory.Exists(target) || !seen.Add(target))
                continue;

            foreach (var file in EnumerateFilesSafe(target))
                AddFile(result, file);
        }
    }

    internal IEnumerable<string> DiscoverProductDirs()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Google"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JetBrains")
        };

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var dir in Directory.EnumerateDirectories(root, "AndroidStudio*",
                         new EnumerationOptions { IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive, RecurseSubdirectories = false }))
            {
                yield return dir;
            }
        }

        // JetBrains per-user product folders in %LOCALAPPDATA%\JetBrains
        var jetBrainsLocal = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains");
        if (Directory.Exists(jetBrainsLocal))
        {
            foreach (var dir in Directory.EnumerateDirectories(jetBrainsLocal, "*",
                         new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false }))
            {
                yield return dir;
            }
        }
    }

    private static void AddFile(ScanResult result, string file)
    {
        try
        {
            var info = new FileInfo(file);
            result.Items.Add(new ScanItem
            {
                FilePath = info.FullName,
                SizeBytes = info.Length,
                IsDeletable = true
            });
        }
        catch
        {
            // skip unreadable file
        }
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            });
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}