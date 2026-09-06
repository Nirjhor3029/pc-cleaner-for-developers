using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface IDiskAnalyzer
{
    /// <summary>
    /// Returns the immediate children of <paramref name="path"/> with each
    /// directory's total recursive size computed. Sorted by size descending.
    /// </summary>
    Task<List<DiskEntry>> GetEntriesAsync(string path, CancellationToken cancellationToken = default);
}