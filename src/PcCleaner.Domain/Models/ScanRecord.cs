namespace PcCleaner.Domain.Models;

public sealed class ScanRecord
{
    public DateTime Date { get; init; } = DateTime.Now;
    public int ScoreBefore { get; init; }
    public int ScoreAfter { get; init; }
    public long FreedBytes { get; init; }
    public SystemSnapshot? Before { get; init; }
    public SystemSnapshot? After { get; init; }
    public List<string> Actions { get; init; } = new();
}
