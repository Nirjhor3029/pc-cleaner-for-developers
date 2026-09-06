using System.Runtime.InteropServices;
using System.Text;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class RecycleBinScanner : IScanner
{
    public string Name => "Recycle Bin";
    public ScannerType Type => ScannerType.RecycleBin;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var items = new List<ScanItem>();

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.Name);

            foreach (var drive in drives)
            {
                var recyclePath = Path.Combine(drive, @"$Recycle.Bin");
                if (!Directory.Exists(recyclePath))
                    continue;

                try
                {
                    var files = Directory.GetFiles(recyclePath, "*.*", new EnumerationOptions
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
                    // Skip if drive recycle bin can't be enumerated
                }
            }
        }
        catch
        {
            // Fall back to empty method if enumeration fails
        }

        return Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = items
        });
    }
}