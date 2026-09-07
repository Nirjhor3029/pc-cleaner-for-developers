using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public sealed class OptimizationItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Label { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public int Count { get; init; }
    public SafetyLevel Safety { get; init; } = SafetyLevel.Safe;
    public bool DefaultChecked { get; init; }
    public bool IsSelected { get; set; }
    public List<string> Paths { get; init; } = new();
    public string Description { get; init; } = string.Empty;
}
