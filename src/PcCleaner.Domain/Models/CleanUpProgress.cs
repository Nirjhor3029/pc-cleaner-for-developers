namespace PcCleaner.Domain.Models;

/// <summary>Reports per-file progress during cleaning.</summary>
public class CleanUpProgress
{
    public int TotalFiles { get; init; }
    public int FilesProcessed { get; init; }
    public string CurrentPath { get; init; } = string.Empty;

    public double Percent => TotalFiles == 0 ? 0 : FilesProcessed * 100.0 / TotalFiles;
}