using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface IHealthScoreService
{
    HealthScore Calculate(SystemSnapshot snapshot);
    List<RecommendedAction> GetRecommendations(HealthScore score, SystemSnapshot snapshot);
}
