using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners;

/// <summary>
/// Base for scanners that list every file under a fixed set of cache directories
/// (missing directories are ignored). Deletion goes through the shared cleaner.
/// </summary>
public abstract class FileListScannerBase : IScanner
{
    public abstract string Name { get; }
    public abstract ScannerType Type { get; }
    public virtual bool RequiresAdmin => false;

    protected abstract IEnumerable<string> Roots();

    public Task<ScanResult> ScanAsync() => ScanRootsAsync(Roots());

    internal Task<ScanResult> ScanRootsAsync(IEnumerable<string> roots)
    {
        return Task.Run(() =>
        {
            var result = new ScanResult { ScannerName = Name, Type = Type };
            foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(root, "*", new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        IgnoreInaccessible = true,
                        ReturnSpecialDirectories = false
                    });
                }
                catch
                {
                    continue;
                }

                foreach (var file in files)
                {
                    try
                    {
                        result.Items.Add(new ScanItem
                        {
                            FilePath = file,
                            SizeBytes = new FileInfo(file).Length,
                            IsDeletable = true
                        });
                    }
                    catch
                    {
                        // skip unreadable file
                    }
                }
            }
            return result;
        });
    }
}
