using System.Diagnostics;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Monitoring;

public sealed class ResourceMonitor : IResourceMonitor
{
    private static readonly HashSet<string> CriticalNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "csrss","wininit","services","lsass","winlogon","smss","svchost","MsMpEng","MsMpEng.exe",
        "System","Registry","Memory Compression","Secure System"
    };

    public async Task<SystemSnapshot> SampleAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var topCpu = GetTopCpu(5);
            var topMem = GetTopMemory(5);
            var snap = new SystemSnapshot
            {
                Timestamp = DateTime.Now,
                TopCpu = topCpu,
                TopMemory = topMem,
                TopDisk = topCpu.OrderByDescending(p => p.DiskBytesPerSec).Take(5).ToList()
            };
            return snap;
        }, ct);
    }

    public async Task<List<ProcessSample>> GetTopCpuAsync(int count = 5, CancellationToken ct = default)
        => await Task.Run(() => GetTopCpu(count), ct);

    public async Task<List<ProcessSample>> GetTopMemoryAsync(int count = 5, CancellationToken ct = default)
        => await Task.Run(() => GetTopMemory(count), ct);

    public async Task<bool> TryEndProcessAsync(int pid, bool gracefulFirst = true)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            if (IsCritical(proc.ProcessName)) return false;

            if (gracefulFirst)
            {
                try
                {
                    if (proc.CloseMainWindow())
                    {
                        await Task.Delay(1500);
                        if (proc.HasExited) return true;
                    }
                }
                catch { }
            }

            proc.Kill(entireProcessTree: true);
            await proc.WaitForExitAsync();
            return true;
        }
        catch { return false; }
    }

    public Task<string?> GetProcessPathAsync(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            return Task.FromResult<string?>(p.MainModule?.FileName);
        }
        catch { return Task.FromResult<string?>(null); }
    }

    private static List<ProcessSample> GetTopCpu(int count)
    {
        var list = new List<ProcessSample>();
        try
        {
            var procs = Process.GetProcesses();
            // Use WorkingSet for initial rough; CPU via next sample would need PerformanceCounter delay — fallback to WorkingSet sort for now with CPU 0
            foreach (var p in procs)
            {
                try
                {
                    list.Add(new ProcessSample
                    {
                        Pid = p.Id,
                        Name = p.ProcessName + ".exe",
                        MemoryBytes = p.WorkingSet64,
                        CpuPercent = 0, // precise per-process CPU requires 2 samples; keep 0 and rely on overall CPU from SystemScanner
                        IsCritical = IsCritical(p.ProcessName),
                        FullPath = SafePath(p)
                    });
                }
                catch { }
            }
        }
        catch { }
        return list.OrderByDescending(x => x.MemoryBytes).Take(count).ToList();
    }

    private static List<ProcessSample> GetTopMemory(int count)
    {
        var list = new List<ProcessSample>();
        try
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    list.Add(new ProcessSample
                    {
                        Pid = p.Id,
                        Name = p.ProcessName + ".exe",
                        MemoryBytes = p.WorkingSet64,
                        CpuPercent = 0,
                        IsCritical = IsCritical(p.ProcessName),
                        FullPath = SafePath(p)
                    });
                }
                catch { }
            }
        }
        catch { }
        return list.OrderByDescending(x => x.MemoryBytes).Take(count).ToList();
    }

    private static bool IsCritical(string name) => CriticalNames.Contains(name) || CriticalNames.Contains(name + ".exe");

    private static string SafePath(Process p)
    {
        try { return p.MainModule?.FileName ?? string.Empty; } catch { return string.Empty; }
    }
}
