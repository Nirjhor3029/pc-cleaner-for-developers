using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public sealed class HealthScore
{
    public int Overall { get; init; }
    public DateTime CalculatedAt { get; init; } = DateTime.Now;
    public List<HealthFactorScore> Factors { get; init; } = new();

    public HealthFactorScore? Get(HealthFactor factor)
        => Factors.FirstOrDefault(f => f.Factor == factor);
}

public sealed class HealthFactorScore
{
    public HealthFactor Factor { get; init; }
    public int Score { get; init; } // 0-100
    public double Weight { get; init; } // 0.20 etc
    public string Reason { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
    public string Status { get; init; } = "Good"; // Good/Warning/Critical
}
