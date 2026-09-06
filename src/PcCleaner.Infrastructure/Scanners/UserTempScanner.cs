using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class UserTempScanner : IScanner
{
    public string Name => "User Temp";
    public ScannerType Type => ScannerType.UserTemp;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var tempPath = Path.GetTempPath();
        return ScanDirectoryAsync(tempPath);
    }

    internal Task<ScanResult> ScanDirectoryAsync(string basePath)
    {
        var items = new List<ScanItem>();

        if (!Directory.Exists(basePath))
            return Task.FromResult(new ScanResult
            {
                ScannerName = Name,
                Type = Type,
                Items = items
            });

        try
        {
            var files = Directory.GetFiles(basePath, "*.*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
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
        catch
        {
            // Skip directories that can't be enumerated
        }

        return Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = items
        });
    }
}