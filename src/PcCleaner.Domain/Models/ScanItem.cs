namespace PcCleaner.Domain.Models;

public class ScanItem
{
    public string FilePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsSelected { get; set; } = true;
    public bool IsDeletable { get; set; } = true;
    public string? Reason { get; set; }
}