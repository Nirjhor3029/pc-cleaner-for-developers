using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public sealed class RecommendedAction
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public HealthFactor Category { get; init; }
    public SafetyLevel Safety { get; init; } = SafetyLevel.Safe;
    public int Severity { get; init; } // 1-3
    public string ActionLabel { get; init; } = string.Empty;
}
