namespace PcCleaner.Domain.Models;

public sealed class ProcessSample
{
    public int Pid { get; init; }
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public double CpuPercent { get; init; }
    public long MemoryBytes { get; init; }
    public double DiskBytesPerSec { get; init; }
    public bool IsCritical { get; init; }
    public bool IsSystem { get; init; }

    public string MemoryDisplay => FormatSize(MemoryBytes);
    public string CpuDisplay => $"{CpuPercent:0.#}%";

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes; int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.#} {sizes[order]}";
    }
}
