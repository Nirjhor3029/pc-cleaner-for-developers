using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the Gradle dependency/download caches under %USERPROFILE%\.gradle:
/// <c>caches</c> (downloaded dependencies + build transforms) and
/// <c>wrapper\dists</c> (downloaded Gradle distributions). Project files and
/// <c>gradle.properties</c> / <c>gradle.projects</c> state are never touched —
/// only the re-downloadable caches.
/// </summary>
public sealed class GradleCacheScanner : IScanner
{
    public string Name => "Gradle Cache";
    public ScannerType Type => ScannerType.GradleCache;
    public bool RequiresAdmin => false;

    private IEnumerable<string> CandidatePaths()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(root, ".gradle", "caches");
        yield return Path.Combine(root, ".gradle", "wrapper", "dists");
    }

    public Task<ScanResult> ScanAsync()
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };

        return Task.Run(() =>
        {
            foreach (var root in CandidatePaths())
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (var file in EnumerateFilesSafe(root))
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
            }
            return result;
        });
    }

    internal Task<ScanResult> ScanPathAsync(string? path)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            foreach (var file in EnumerateFilesSafe(path))
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