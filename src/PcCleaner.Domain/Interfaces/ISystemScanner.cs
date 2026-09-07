using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface ISystemScanner
{
    Task<SystemSnapshot> ScanAsync(CancellationToken ct = default);
}
