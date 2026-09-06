using System.Windows;
using PcCleaner.Domain.Models;

namespace PcCleaner.App;

public class DiskEntryViewModel
{
    public DiskEntry Entry { get; }
    public string Name => Entry.Name;
    public string FullPath => Entry.FullPath;
    public bool IsDirectory => Entry.IsDirectory;
    public long SizeBytes => Entry.SizeBytes;
    public long FileCount => Entry.FileCount;
    public bool IsProtected => Entry.IsProtected;

    public string TypeIcon => IsDirectory ? "📁" : "📄";
    public string SizeDisplay => FormatSize(SizeBytes);
    public string FileCountDisplay => IsDirectory ? $"{FileCount:N0} files" : "";
    public double BarPercent { get; set; }
    public string BarPercentText => $"{BarPercent:0.#}%";

    public string TagText => IsProtected ? "SYSTEM" : "";
    public Visibility TagVisibility => IsProtected ? Visibility.Visible : Visibility.Collapsed;

    public DiskEntryViewModel(DiskEntry entry)
    {
        Entry = entry;
    }

    public static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}