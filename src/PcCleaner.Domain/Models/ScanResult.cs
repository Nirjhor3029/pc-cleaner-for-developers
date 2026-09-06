using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public class ScanResult
{
    public string ScannerName { get; set; } = string.Empty;
    public ScannerType Type { get; set; }
    public List<ScanItem> Items { get; set; } = new();
    public long TotalSizeBytes => Items.Sum(i => i.SizeBytes);
    public int ItemCount => Items.Count;
}