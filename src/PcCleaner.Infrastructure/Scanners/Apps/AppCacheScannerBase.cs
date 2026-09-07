using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Apps;

/// <summary>
/// Base class for communication-app caches (Teams, Slack, Discord). Only the
/// known chrome-style cache subfolders of the app's data directory are scanned
/// (Cache, Code Cache, GPUCache, …); profile data such as Local Storage,
/// IndexedDB and sqlite databases are never touched.
/// </summary>
public abstract class AppCacheScannerBase : IScanner
{
    public abstract string Name { get; }
    public abstract ScannerType Type { get; }
    public virtual bool RequiresAdmin => false;

    /// <summary>Ordered candidate product data directories; the first existing one is used.</summary>
    protected abstract IEnumerable<string> CandidatePaths();

    /// <summary>Cache subfolders within the product directory that are safe to clean.</summary>
    protected abstract string[] CacheSubdirs { get; }

    public Task<ScanResult> ScanAsync()
    {
        foreach (var p in CandidatePaths())
        {
            if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                return ScanPathAsync(p);
        }

        return Task.FromResult(new ScanResult { ScannerName = Name, Type = Type });
    }

    /// <summary>Testable entry point: scan the cache subfolders of a given product dir.</summary>
    internal Task<ScanResult> ScanPathAsync(string? productDir)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (string.IsNullOrEmpty(productDir) || !Directory.Exists(productDir))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            foreach (var sub in CacheSubdirs)
            {
                var target = Path.Combine(productDir, sub);
                if (!Directory.Exists(target))
                    continue;

                foreach (var file in EnumerateFilesSafe(target))
                    AddFile(result, file);
            }
            return result;
        });
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