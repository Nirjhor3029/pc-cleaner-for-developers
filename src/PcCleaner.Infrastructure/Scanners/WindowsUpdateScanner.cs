using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class WindowsUpdateScanner : IScanner
{
    public string Name => "Windows Update Cache";
    public ScannerType Type => ScannerType.WindowsUpdateCache;
    public bool RequiresAdmin => true;

    private static readonly string[] UpdatePaths = new[]
    {
        @"C:\Windows\SoftwareDistribution\Download",
        @"C:\Windows\SoftwareDistribution\DeliveryOptimization"
    };

    public Task<ScanResult> ScanAsync()
    {
        var items = new List<ScanItem>();

        if (!IsServiceStoppedOrAccessible())
        {
            return Task.FromResult(new ScanResult
            {
                ScannerName = Name,
                Type = Type,
                Items = items
            });
        }

        foreach (var updatePath in UpdatePaths)
        {
            if (!Directory.Exists(updatePath))
                continue;

            try
            {
                var files = Directory.GetFiles(updatePath, "*.*", new EnumerationOptions
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
                        // Skip locked/denied files
                    }
                }
            }
            catch
            {
                // Skip paths that can't be enumerated
            }
        }

        return Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = items
        });
    }

    private static bool IsServiceStoppedOrAccessible()
    {
        // Early check: if we can't even read the directory, return false
        try
        {
            var download = UpdatePaths.FirstOrDefault(Directory.Exists);
            if (download != null && !IsAccessible(download))
                return false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsAccessible(string path)
    {
        try
        {
            Directory.GetFiles(path).Count();
            return true;
        }
        catch
        {
            return false;
        }
    }
}