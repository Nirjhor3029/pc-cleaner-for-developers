namespace PcCleaner.Domain.Models;

public class DiskEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long SizeBytes { get; set; }
    public long FileCount { get; set; }
    public bool IsProtected { get; set; }
    public List<DiskEntry> Children { get; set; } = new();
}