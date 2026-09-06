using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using Serilog;

namespace PcCleaner.Infrastructure.Cleaning;

public class CleaningEngine : ICleaner
{
    public CleaningEngine()
    {
    }

    public Task<CleaningResult> CleanAsync(List<ScanItem> items)
    {
        return CleanAsync(items, null);
    }

    public Task<CleaningResult> CleanAsync(List<ScanItem> items, IProgress<CleanUpProgress>? progress)
    {
        Log.Information("Cleaning started: {Count} items selected, total {Size:N0} bytes",
            items.Count, items.Sum(i => i.SizeBytes));

        var result = new CleaningResult
        {
            FoundBytes = items.Sum(i => i.SizeBytes)
        };

        var toProcess = items.Where(i => i.IsSelected).ToList();
        int total = toProcess.Count;
        int processed = 0;

        foreach (var item in toProcess)
        {
            processed++;

            if (!item.IsDeletable)
            {
                result.FilesSkipped++;
                result.SkippedBytes += item.SizeBytes;
                result.SkippedReasons.Add($"{item.FilePath}: Marked as non-deletable");
                ReportProgress(progress, total, processed, item.FilePath);
                continue;
            }

            try
            {
                if (!File.Exists(item.FilePath) && !Directory.Exists(item.FilePath))
                {
                    result.FilesSkipped++;
                    result.SkippedBytes += item.SizeBytes;
                    result.SkippedReasons.Add($"{item.FilePath}: File not found");
                    ReportProgress(progress, total, processed, item.FilePath);
                    continue;
                }

                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                else
                {
                    Directory.Delete(item.FilePath, true);
                }

                result.FilesDeleted++;
                result.RemovedBytes += item.SizeBytes;
            }
            catch (UnauthorizedAccessException)
            {
                result.FilesSkipped++;
                result.SkippedBytes += item.SizeBytes;
                result.SkippedReasons.Add($"{item.FilePath}: Access denied");
            }
            catch (IOException)
            {
                result.FilesSkipped++;
                result.SkippedBytes += item.SizeBytes;
                result.SkippedReasons.Add($"{item.FilePath}: File in use");
            }
            catch (Exception ex)
            {
                result.FilesSkipped++;
                result.SkippedBytes += item.SizeBytes;
                result.SkippedReasons.Add($"{item.FilePath}: {ex.Message}");
            }

            ReportProgress(progress, total, processed, item.FilePath);
        }

        Log.Information("Cleaning complete: deleted={Deleted}, skipped={Skipped}, removed={Removed:N0} bytes, skipped bytes={SkippedBytes:N0}",
            result.FilesDeleted, result.FilesSkipped, result.RemovedBytes, result.SkippedBytes);

        return Task.FromResult(result);
    }

    private static void ReportProgress(IProgress<CleanUpProgress>? progress, int total, int processed, string path)
    {
        if (progress == null)
            return;

        // Throttle UI updates: report every 25 files or on the final file
        if (processed % 25 != 0 && processed != total)
            return;

        progress.Report(new CleanUpProgress
        {
            TotalFiles = total,
            FilesProcessed = processed,
            CurrentPath = path
        });
    }
}