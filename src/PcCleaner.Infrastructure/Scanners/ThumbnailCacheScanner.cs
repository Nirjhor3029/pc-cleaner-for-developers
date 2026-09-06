using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class ThumbnailCacheScanner : IScanner
{
    public string Name => "Thumbnail Cache";
    public ScannerType Type => ScannerType.ThumbnailCache;
    public bool RequiresAdmin => false;

    private static readonly string ThumbnailPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Microsoft\Windows\Explorer");

    private static readonly string[] CachePatterns = { "thumbcache_*.db", "iconcache_*.db" };

    public Task<ScanResult> ScanAsync()
    {
        var items = new List<ScanItem>();

        if (!Directory.Exists(ThumbnailPath))
            return Task.FromResult(new ScanResult
            {
                ScannerName = Name,
                Type = Type,
                Items = items
            });

        try
        {
            foreach (var pattern in CachePatterns)
            {
                var files = Directory.GetFiles(ThumbnailPath, pattern, new EnumerationOptions
                {
                    RecurseSubdirectories = false,
                    IgnoreInaccessible = true,
                    ReturnSpecialDirectories = false
                });

                foreach (var file in files)
                {
                    try
                    {
                        var info = new FileInfo(file);
                        items.Add(new ScanItem
                        {
                            FilePath = file,
                            SizeBytes = info.Length,
                            IsSelected = true,
                            IsDeletable = true
                        });
                    }
                    catch
                    {
                        // Skip files that can't be accessed
                    }
                }
            }
        }
        catch
        {
            // Skip if directory can't be enumerated
        }

        return Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = items
        });
    }
}