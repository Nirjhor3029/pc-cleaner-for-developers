using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Base class for scanners that clean a single well-known cache directory
/// (package-manager caches such as npm, yarn, composer). Only the cache
/// directory is scanned; project files are never touched. Deletion is left
/// to the shared <see cref="ICleaner"/>.
/// </summary>
public abstract class DevelopersCacheScannerBase : IScanner
{
    public abstract string Name { get; }
    public abstract ScannerType Type { get; }
    public virtual bool RequiresAdmin => false;

    /// <summary>Ordered candidate cache paths; the first existing one is used.</summary>
    protected abstract IEnumerable<string> CandidatePaths();

    public Task<ScanResult> ScanAsync()
    {
        return ScanPathAsync(FindExistingPath());
    }

    /// <summary>
    /// Testable entry point: scan a specific cache directory directly.
    /// Passing a missing/null path yields an empty result (never throws).
    /// </summary>
    internal Task<ScanResult> ScanPathAsync(string? cachePath)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            foreach (var file in SafeEnumerateFiles(cachePath))
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
            return result;
        });
    }

    internal string? FindExistingPath()
    {
        foreach (var p in CandidatePaths())
        {
            if (!string.IsNullOrEmpty(p) && Directory.Exists(p))
                return p;
        }
        return null;
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", new EnumerationOptions
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