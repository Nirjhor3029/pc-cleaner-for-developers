using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

public class CrashDumpScanner : IScanner
{
    public string Name => "Crash Dumps";
    public ScannerType Type => ScannerType.CrashDumps;
    public bool RequiresAdmin => false;

    private static readonly string[] DumpPaths = new[]
    {
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"CrashDumps"),
        @"C:\Windows\Minidump",
        @"C:\Windows\MEMORY.DMP"
    };

    public Task<ScanResult> ScanAsync()
    {
        var items = new List<ScanItem>();

        foreach (var dumpPath in DumpPaths)
        {
            try
            {
                if (File.Exists(dumpPath))
                {
                    var info = new FileInfo(dumpPath);
                    items.Add(new ScanItem
                    {
                        FilePath = dumpPath,
                        SizeBytes = info.Length,
                        IsSelected = true,
                        IsDeletable = true
                    });
                }
                else if (Directory.Exists(dumpPath))
                {
                    var files = Directory.GetFiles(dumpPath, "*.*", new EnumerationOptions
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
            }
            catch
            {
                // Skip paths that can't be accessed
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