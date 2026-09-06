using System.Diagnostics;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Browser;

/// <summary>
/// Base for Chromium-based browser cache scanners (Chrome, Edge).
/// Discovers every profile directory (Default, Profile 1, Profile 2, ...)
/// under the browser's User Data root and scans only the safe cache
/// subfolders: Cache, Code Cache, GPUCache. Bookmarks, history, cookies,
/// passwords, extensions, and settings are never touched.
/// </summary>
public abstract class BrowserCacheScannerBase : IScanner
{
    private static readonly string[] CacheSubDirs = { "Cache", "Code Cache", "GPUCache" };

    public abstract string Name { get; }
    public abstract ScannerType Type { get; }
    public virtual bool RequiresAdmin => false;

    protected abstract string BrowserUserDataRoot();
    protected abstract bool IsBrowserRunning();

    public Task<ScanResult> ScanAsync()
    {
        return ScanProfilesAsync(BrowserUserDataRoot(), IsBrowserRunning());
    }

    internal Task<ScanResult> ScanProfilesAsync(string userDataRoot, bool browserRunning = false)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (!Directory.Exists(userDataRoot))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            TryAddProfileDir(Path.Combine(userDataRoot, "Default"), browserRunning, result);
            TryAddProfileDir(Path.Combine(userDataRoot, "Guest Profile"), browserRunning, result);

            foreach (var profileDir in SafeEnumerateDirectories(userDataRoot))
            {
                var name = Path.GetFileName(profileDir);
                if (!name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                    continue;
                TryAddProfileDir(profileDir, browserRunning, result);
            }
            return result;
        });
    }

    private static void TryAddProfileDir(string profileDir, bool browserRunning, ScanResult result)
    {
        if (!Directory.Exists(profileDir))
            return;

        foreach (var cacheSub in CacheSubDirs)
        {
            var cachePath = Path.Combine(profileDir, cacheSub);
            if (!Directory.Exists(cachePath))
                continue;

            foreach (var file in SafeEnumerateFiles(cachePath))
            {
                try
                {
                    var info = new FileInfo(file);
                    result.Items.Add(new ScanItem
                    {
                        FilePath = info.FullName,
                        SizeBytes = info.Length,
                        IsSelected = true,
                        IsDeletable = !browserRunning
                    });
                }
                catch
                {
                    // skip file that can't be accessed
                }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            });
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = false,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            });
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}