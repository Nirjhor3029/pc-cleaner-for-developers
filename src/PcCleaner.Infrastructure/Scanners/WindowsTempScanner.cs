using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class WindowsTempScanner : IScanner
{
    public string Name => "Windows Temp";
    public ScannerType Type => ScannerType.WindowsTemp;
    public bool RequiresAdmin => true;

    private static readonly string WindowsTempPath = @"C:\Windows\Temp";

    public Task<ScanResult> ScanAsync()
    {
        var items = new List<ScanItem>();

        if (!Directory.Exists(WindowsTempPath))
            return Task.FromResult(new ScanResult
            {
                ScannerName = Name,
                Type = Type,
                Items = items
            });

        try
        {
            var files = Directory.GetFiles(WindowsTempPath, "*.*", new EnumerationOptions
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
            // Skip if directory can't be enumerated (permission issue)
        }

        return Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = items
        });
    }
}