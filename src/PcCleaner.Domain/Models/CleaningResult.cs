namespace PcCleaner.Domain.Models;

public class CleaningResult
{
    public long FoundBytes { get; set; }
    public long RemovedBytes { get; set; }
    public long SkippedBytes { get; set; }
    public int FilesDeleted { get; set; }
    public int FilesSkipped { get; set; }
    public List<string> SkippedReasons { get; set; } = new();
}