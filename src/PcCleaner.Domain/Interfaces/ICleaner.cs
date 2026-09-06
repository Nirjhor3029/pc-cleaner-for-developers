using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface ICleaner
{
    Task<CleaningResult> CleanAsync(List<ScanItem> items);

    Task<CleaningResult> CleanAsync(List<ScanItem> items, IProgress<CleanUpProgress>? progress);
}