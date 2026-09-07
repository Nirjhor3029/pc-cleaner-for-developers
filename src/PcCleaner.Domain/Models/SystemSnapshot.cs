namespace PcCleaner.Domain.Models;

public sealed class SystemSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;

    // CPU
    public double CpuPercent { get; init; }
    public string CpuModel { get; init; } = string.Empty;
    public int CpuCores { get; init; }
    public int CpuThreads { get; init; }
    public bool CpuThrottled { get; init; }

    // RAM
    public long RamTotalBytes { get; init; }
    public long RamUsedBytes { get; init; }
    public long RamAvailableBytes { get; init; }
    public double RamPercent => RamTotalBytes == 0 ? 0 : (double)RamUsedBytes / RamTotalBytes * 100;

    // Disk / Storage (C:)
    public long StorageTotalBytes { get; init; }
    public long StorageUsedBytes { get; init; }
    public long StorageFreeBytes { get; init; }
    public double StorageFreePercent => StorageTotalBytes == 0 ? 0 : (double)StorageFreeBytes / StorageTotalBytes * 100;
    public string DiskType { get; init; } = "Unknown"; // SSD/HDD/NVMe
    public double DiskActivePercent { get; init; } // 0-100
    public string DriveLetter { get; init; } = "C:\\";

    // Startup
    public int StartupCount { get; init; }
    public int StartupHighImpactCount { get; init; }

    // Processes
    public List<ProcessSample> TopCpu { get; init; } = new();
    public List<ProcessSample> TopMemory { get; init; } = new();
    public List<ProcessSample> TopDisk { get; init; } = new();
}
