using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Commands;
using Serilog;

namespace PcCleaner.Infrastructure.Cleaning;

public class CleaningEngine : ICleaner
{
    private readonly ICommandRunner _commandRunner;

    public CleaningEngine() : this(new ProcessCommandRunner())
    {
    }

    public CleaningEngine(ICommandRunner commandRunner)
    {
        _commandRunner = commandRunner;
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

            if (item.Command != null)
            {
                // Commands can run for minutes; show which one before it starts
                progress?.Report(new CleanUpProgress
                {
                    TotalFiles = total,
                    FilesProcessed = processed - 1,
                    CurrentPath = item.FilePath
                });
                RunCommand(item, result);
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

    private void RunCommand(ScanItem item, CleaningResult result)
    {
        var command = item.Command!;
        Log.Information("Running cleanup command: {File} {Args}", command.FileName, command.Arguments);

        try
        {
            var outcome = _commandRunner.Run(command.FileName, command.Arguments);
            if (outcome.Succeeded)
            {
                result.FilesDeleted++;
                result.RemovedBytes += item.SizeBytes;
                return;
            }

            Log.Warning("Cleanup command failed ({Code}): {Error}", outcome.ExitCode, outcome.StandardError.Trim());
            result.FilesSkipped++;
            result.SkippedBytes += item.SizeBytes;
            result.SkippedReasons.Add($"{item.FilePath}: command failed (exit code {outcome.ExitCode})");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Cleanup command could not be started");
            result.FilesSkipped++;
            result.SkippedBytes += item.SizeBytes;
            result.SkippedReasons.Add($"{item.FilePath}: {ex.Message}");
        }
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