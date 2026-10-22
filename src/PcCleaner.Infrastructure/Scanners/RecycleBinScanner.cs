using System.Security.Principal;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

/// <summary>
/// Scans the current user's Recycle Bin on every fixed drive
/// (<c>X:\$Recycle.Bin\&lt;user SID&gt;</c>). The SID folders are Hidden+System,
/// so the default <see cref="EnumerationOptions"/> (which skip those attributes)
/// cannot be used. Other users' bins are never touched.
/// </summary>
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
            using var identity = WindowsIdentity.GetCurrent();
            var sid = identity.User?.Value;
            if (string.IsNullOrEmpty(sid))
                return Task.FromResult(new ScanResult { ScannerName = Name, Type = Type, Items = items });

            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.Name);

            foreach (var drive in drives)
                items.AddRange(ScanBinDirectory(Path.Combine(drive, "$Recycle.Bin", sid)));
        }
        catch
        {
            // Fall back to empty result if enumeration fails
        }

        return Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = items
        });
    }

    internal static List<ScanItem> ScanBinDirectory(string binPath)
    {
        var items = new List<ScanItem>();
        if (!Directory.Exists(binPath))
            return items;

        try
        {
            // Each recycled item is a top-level $I (metadata) / $R (content) pair;
            // a recycled folder is a $R directory, so delete it as a whole.
            foreach (var entry in Directory.EnumerateFileSystemEntries(binPath, "*", BinOptions(recurse: false)))
            {
                // desktop.ini gives the folder its Recycle Bin shell behaviour
                if (string.Equals(Path.GetFileName(entry), "desktop.ini", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    long size = Directory.Exists(entry)
                        ? Directory.EnumerateFiles(entry, "*", BinOptions(recurse: true)).Sum(f => new FileInfo(f).Length)
                        : new FileInfo(entry).Length;

                    items.Add(new ScanItem
                    {
                        FilePath = entry,
                        SizeBytes = size,
                        IsSelected = true,
                        IsDeletable = true
                    });
                }
                catch
                {
                    // Skip entries that can't be accessed
                }
            }
        }
        catch
        {
            // Skip if the bin can't be enumerated
        }

        return items;
    }

    private static EnumerationOptions BinOptions(bool recurse) => new()
    {
        RecurseSubdirectories = recurse,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };
}
