using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface IScanHistoryStore
{
    Task<List<ScanRecord>> LoadAsync();
    Task AppendAsync(ScanRecord record);
    Task ClearAsync();
}
