namespace PcCleaner.Application.Models;

/// <summary>Reports category-level progress while running scanners.</summary>
public class ScanProgress
{
    public int CurrentIndex { get; init; }
    public int Total { get; init; }
    public string CategoryName { get; init; } = string.Empty;

    public double Percent => Total == 0 ? 0 : CurrentIndex * 100.0 / Total;
}