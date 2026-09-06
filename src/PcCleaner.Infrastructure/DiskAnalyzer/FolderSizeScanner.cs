using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.DiskAnalyzer;

public class FolderSizeScanner : IDiskAnalyzer
{
    private static EnumerationOptions EnumerateOptions => new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    public Task<List<DiskEntry>> GetEntriesAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Scan(path, cancellationToken), cancellationToken);
    }

    private static List<DiskEntry> Scan(string path, CancellationToken ct)
    {
        var result = new List<DiskEntry>();

        if (!Directory.Exists(path))
            return result;

        foreach (var file in Directory.EnumerateFiles(path, "*", EnumerateOptions))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(file);
                result.Add(new DiskEntry
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = false,
                    SizeBytes = info.Length,
                    FileCount = 1,
                    IsProtected = info.Attributes.HasFlag(FileAttributes.System)
                });
            }
            catch
            {
                // skip unreadable file
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(path, "*", EnumerateOptions))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new DirectoryInfo(dir);
                var (size, count) = ComputeSize(dir, ct);
                result.Add(new DiskEntry
                {
                    Name = info.Name,
                    FullPath = info.FullName,
                    IsDirectory = true,
                    SizeBytes = size,
                    FileCount = count,
                    IsProtected = info.Attributes.HasFlag(FileAttributes.System)
                });
            }
            catch
            {
                // skip unreadable directory
            }
        }

        return result
            .OrderByDescending(e => e.SizeBytes)
            .ToList();
    }

    private static (long Size, long Count) ComputeSize(string dir, CancellationToken ct)
    {
        long size = 0;
        long count = 0;

        foreach (var file in Directory.EnumerateFiles(dir, "*", EnumerateOptions))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                size += new FileInfo(file).Length;
                count++;
            }
            catch
            {
                // skip unreadable file
            }
        }

        foreach (var sub in Directory.EnumerateDirectories(dir, "*", EnumerateOptions))
        {
            ct.ThrowIfCancellationRequested();
            var (subSize, subCount) = ComputeSize(sub, ct);
            size += subSize;
            count += subCount;
        }

        return (size, count);
    }
}