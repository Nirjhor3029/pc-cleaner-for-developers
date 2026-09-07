using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans downloaded AI/model caches under %USERPROFILE%\.cache\huggingface
/// (models, datasets, hub blobs). These are re-downloadable artifacts consumed
/// by ML tooling. Chat / assistant session history is deliberately preserved
/// and never scanned.
/// </summary>
public sealed class HuggingFaceCacheScanner : IScanner
{
    public string Name => "AI Model Cache (Hugging Face)";
    public ScannerType Type => ScannerType.HuggingFaceCache;
    public bool RequiresAdmin => false;

    private string? CandidatePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(root, ".cache", "huggingface");
    }

    public Task<ScanResult> ScanAsync() => ScanPathAsync(CandidatePath());

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