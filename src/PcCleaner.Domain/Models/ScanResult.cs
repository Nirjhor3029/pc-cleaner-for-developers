using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public class ScanResult
{
    public string ScannerName { get; set; } = string.Empty;
    public ScannerType Type { get; set; }
    public List<ScanItem> Items { get; set; } = new();
    public long TotalSizeBytes => Items.Sum(i => i.SizeBytes);
    public int ItemCount => Items.Count;

    /// <summary>Copied from the scanner by the scan service.</summary>
    public bool RequiresAdmin { get; set; }

    /// <summary>Risky or slow categories are shown unchecked.</summary>
    public bool SelectedByDefault { get; set; } = true;

    /// <summary>Shown under the category name, e.g. "⚠ Re-downloaded on next run".</summary>
    public string? Warning { get; set; }

    /// <summary>True when the reclaimable size can't be estimated before cleaning (e.g. DISM).</summary>
    public bool SizeUnknown { get; set; }
}
