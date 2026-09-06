using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Cleans downloaded Playwright browser binaries under the per-user
/// ms-playwright folder. Deleting these means browsers must be re-downloaded
/// on the next run, so this is surfaced as a warning in the UI.
/// </summary>
public class PlaywrightCacheScanner : IScanner
{
    public string Name => "Playwright Browsers";
    public ScannerType Type => ScannerType.PlaywrightCache;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ms-playwright");
        return ScanDirectoryAsync(root);
    }

    internal Task<ScanResult> ScanDirectoryAsync(string root)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (!Directory.Exists(root))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root))
                {
                    foreach (var file in SafeEnumerateFiles(dir))
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
            }
            catch
            {
                // ignore enumeration errors
            }
            return result;
        });
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