using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.SystemInfo;

public sealed class SystemScanner : ISystemScanner
{
    public async Task<SystemSnapshot> ScanAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var snap = new SystemSnapshot
            {
                Timestamp = DateTime.Now,
                CpuPercent = GetCpuUsage(),
                CpuModel = GetCpuModel(),
                CpuCores = Environment.ProcessorCount,
                CpuThreads = Environment.ProcessorCount,
                RamTotalBytes = GetTotalRam(),
                DriveLetter = "C:\\"
            };

            try
            {
                var totalRam = GetTotalRam();
                var availRam = GetAvailableRam();
                snap = new SystemSnapshot
                {
                    Timestamp = snap.Timestamp,
                    CpuPercent = snap.CpuPercent,
                    CpuModel = snap.CpuModel,
                    CpuCores = snap.CpuCores,
                    CpuThreads = snap.CpuThreads,
                    RamTotalBytes = totalRam,
                    RamAvailableBytes = availRam,
                    RamUsedBytes = totalRam - availRam,
                    DriveLetter = snap.DriveLetter,
                    StorageTotalBytes = GetDriveTotal("C"),
                    StorageFreeBytes = GetDriveFree("C"),
                    StorageUsedBytes = GetDriveTotal("C") - GetDriveFree("C"),
                    DiskType = GetDiskType(),
                    DiskActivePercent = GetDiskActivePercent(),
                };
                // Startup counts filled by caller via IStartupManager
            }
            catch { }

            var total = snap.RamTotalBytes;
            var avail = snap.RamAvailableBytes;
            snap = new SystemSnapshot
            {
                Timestamp = snap.Timestamp,
                CpuPercent = snap.CpuPercent,
                CpuModel = snap.CpuModel,
                CpuCores = snap.CpuCores,
                CpuThreads = snap.CpuThreads,
                RamTotalBytes = snap.RamTotalBytes,
                RamUsedBytes = total - avail,
                RamAvailableBytes = avail,
                DriveLetter = snap.DriveLetter,
                StorageTotalBytes = snap.StorageTotalBytes,
                StorageUsedBytes = snap.StorageUsedBytes,
                StorageFreeBytes = snap.StorageFreeBytes,
                DiskType = snap.DiskType,
                DiskActivePercent = snap.DiskActivePercent,
                StartupCount = snap.StartupCount,
                StartupHighImpactCount = snap.StartupHighImpactCount,
                TopCpu = snap.TopCpu,
                TopMemory = snap.TopMemory,
                TopDisk = snap.TopDisk
            };

            return snap;
        }, ct);
    }

    private static double GetCpuUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("select LoadPercentage from Win32_Processor");
            double total = 0; int count = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["LoadPercentage"] != null) { total += Convert.ToDouble(obj["LoadPercentage"]); count++; }
            }
            return count == 0 ? 0 : total / count;
        }
        catch { return 0; }
    }

    private static string GetCpuModel()
    {
        try
        {
            using var s = new ManagementObjectSearcher("select Name from Win32_Processor");
            foreach (ManagementObject o in s.Get()) return o["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
        }
        catch { }
        return "Unknown CPU";
    }

    private static long GetTotalRam()
    {
        try
        {
            using var s = new ManagementObjectSearcher("select TotalPhysicalMemory from Win32_ComputerSystem");
            foreach (ManagementObject o in s.Get()) return Convert.ToInt64(o["TotalPhysicalMemory"]);
        }
        catch { }
        return 8L * 1024 * 1024 * 1024;
    }

    private static long GetAvailableRam()
    {
        try
        {
            using var s = new ManagementObjectSearcher("select FreePhysicalMemory from Win32_OperatingSystem");
            foreach (ManagementObject o in s.Get()) return Convert.ToInt64(o["FreePhysicalMemory"]) * 1024;
        }
        catch { }
        return 0;
    }

    private static long GetDriveTotal(string letter)
    {
        try { var d = new DriveInfo(letter); return d.IsReady ? d.TotalSize : 0; } catch { return 0; }
    }
    private static long GetDriveFree(string letter)
    {
        try { var d = new DriveInfo(letter); return d.IsReady ? d.AvailableFreeSpace : 0; } catch { return 0; }
    }

    private static string GetDiskType()
    {
        try
        {
            using var s = new ManagementObjectSearcher("select MediaType, Model from Win32_DiskDrive");
            foreach (ManagementObject o in s.Get())
            {
                var model = o["Model"]?.ToString() ?? "";
                if (model.Contains("SSD", StringComparison.OrdinalIgnoreCase) || model.Contains("NVMe", StringComparison.OrdinalIgnoreCase)) return "SSD";
                if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase)) return "NVMe";
            }
            return "SSD";
        }
        catch { return "Unknown"; }
    }

    private static double GetDiskActivePercent()
    {
        try
        {
            using var s = new ManagementObjectSearcher("select PercentDiskTime from Win32_PerfFormattedData_PerfDisk_PhysicalDisk where Name='_Total'");
            foreach (ManagementObject o in s.Get()) return Convert.ToDouble(o["PercentDiskTime"]);
        }
        catch { }
        return 0;
    }
}
