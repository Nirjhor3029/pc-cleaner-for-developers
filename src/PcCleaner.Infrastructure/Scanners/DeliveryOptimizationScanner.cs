using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class DeliveryOptimizationScanner : IScanner
{
    public string Name => "Delivery Optimization";
    public ScannerType Type => ScannerType.DeliveryOptimization;
    public bool RequiresAdmin => true;

    private static readonly string[] OptimizationPaths = new[]
    {
        @"C:\Windows\ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache",
        @"C:\ProgramData\Microsoft\Windows\DeliveryOptimization\Cache"
    };

    public Task<ScanResult> ScanAsync()
    {
        var items = new List<ScanItem>();

        foreach (var path in OptimizationPaths)
        {
            if (!Directory.Exists(path))
                continue;

            try
            {
                var files = Directory.GetFiles(path, "*.*", new EnumerationOptions
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
}