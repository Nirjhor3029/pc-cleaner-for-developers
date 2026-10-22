using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface IResourceMonitor
{
    Task<SystemSnapshot> SampleAsync(CancellationToken ct = default);
    Task<List<ProcessSample>> GetTopCpuAsync(int count = 5, CancellationToken ct = default);
    Task<List<ProcessSample>> GetTopMemoryAsync(int count = 5, CancellationToken ct = default);
    Task<bool> TryEndProcessAsync(int pid, bool gracefulFirst = true);
    Task<string?> GetProcessPathAsync(int pid);
}
