using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Health;

public sealed class HealthScoreService : IHealthScoreService
{
    public HealthScore Calculate(SystemSnapshot s)
    {
        var factors = new List<HealthFactorScore>
        {
            ScoreStorage(s),
            ScoreStartup(s),
            ScoreMemory(s),
            ScoreCpu(s),
            ScoreDisk(s),
            ScoreBackground(s)
        };

        int overall = (int)Math.Round(factors.Sum(f => f.Score * f.Weight));
        overall = Math.Clamp(overall, 0, 100);

        return new HealthScore { Overall = overall, Factors = factors, CalculatedAt = DateTime.Now };
    }

    public List<RecommendedAction> GetRecommendations(HealthScore score, SystemSnapshot s)
    {
        var list = new List<RecommendedAction>();
        foreach (var f in score.Factors.Where(f => f.Score < 75))
        {
            list.Add(new RecommendedAction
            {
                Title = $"{f.Factor} needs attention",
                Message = f.Reason,
                Category = f.Factor,
                Safety = f.Score < 40 ? SafetyLevel.Caution : SafetyLevel.Safe,
                Severity = f.Score < 40 ? 3 : 2,
                ActionLabel = f.Factor == HealthFactor.Storage ? "Free up storage" :
                              f.Factor == HealthFactor.Startup ? "Manage startup" :
                              f.Factor == HealthFactor.Memory ? "View processes" : "Optimize"
            });
        }

        // Additional heuristic: high process consumers
        if (s.TopMemory.FirstOrDefault()?.MemoryBytes > 2L * 1024 * 1024 * 1024)
        {
            var top = s.TopMemory.First();
            list.Add(new RecommendedAction
            {
                Title = $"{top.Name} is using {top.MemoryDisplay} RAM",
                Message = $"{top.Name} is the top memory consumer.",
                Category = HealthFactor.Background,
                Safety = SafetyLevel.Safe,
                Severity = 2,
                ActionLabel = "View processes"
            });
        }

        if (s.StorageFreePercent < 10 && !list.Any(a => a.Category == HealthFactor.Storage))
        {
            list.Add(new RecommendedAction
            {
                Title = $"C: drive has only {s.StorageFreeBytes / (1024*1024*1024)} GB free ({s.StorageFreePercent:0.#}%)",
                Message = "Low free space can slow down Windows and updates.",
                Category = HealthFactor.Storage,
                Safety = SafetyLevel.Safe,
                Severity = 3,
                ActionLabel = "Clean"
            });
        }

        return list.OrderByDescending(a => a.Severity).Take(5).ToList();
    }

    private static HealthFactorScore ScoreStorage(SystemSnapshot s)
    {
        double free = s.StorageFreePercent;
        int score; string reason; string rec; string status;
        if (free < 5) { score = 15; reason = $"C: drive has only {free:0.#}% free ({s.StorageFreeBytes / (1024*1024*1024)} GB). Critical low space."; rec = "Free up storage by cleaning temp/cache and large files."; status = "Critical"; }
        else if (free < 10) { score = 40; reason = $"C: drive has {free:0.#}% free. Low space warning."; rec = "Free up storage."; status = "Warning"; }
        else if (free < 20) { score = 65; reason = $"C: drive has {free:0.#}% free. Moderate."; rec = "Consider cleaning junk files."; status = "Warning"; }
        else { score = 90; reason = $"C: drive has {free:0.#}% free. Healthy."; rec = "No action needed."; status = "Good"; }
        return new HealthFactorScore { Factor = HealthFactor.Storage, Score = score, Weight = 0.20, Reason = reason, Recommendation = rec, Status = status };
    }

    private static HealthFactorScore ScoreStartup(SystemSnapshot s)
    {
        int count = s.StartupCount; int high = s.StartupHighImpactCount;
        int score; string reason; string rec; string status;
        if (count >= 10 || high >= 4) { score = 25; reason = $"{count} apps start automatically. {high} have high startup impact."; rec = "Disable unnecessary startup applications."; status = "Critical"; }
        else if (count >= 7 || high >= 2) { score = 52; reason = $"{count} applications start automatically. {high} have high startup impact."; rec = "Disable 2-3 startup apps."; status = "Warning"; }
        else if (count >= 4) { score = 75; reason = $"{count} startup apps, {high} high impact."; rec = "Review startup apps."; status = "Warning"; }
        else { score = 90; reason = $"{count} startup apps. Lean boot."; rec = "No action needed."; status = "Good"; }
        return new HealthFactorScore { Factor = HealthFactor.Startup, Score = score, Weight = 0.20, Reason = reason, Recommendation = rec, Status = status };
    }

    private static HealthFactorScore ScoreMemory(SystemSnapshot s)
    {
        double pct = s.RamPercent;
        int score; string status; string reason; string rec;
        if (pct >= 90) { score = 30; status = "Critical"; reason = $"RAM usage is {pct:0.#}% ({s.RamUsedBytes / (1024*1024*1024)} / {s.RamTotalBytes / (1024*1024*1024)} GB). High memory pressure."; rec = "Close unnecessary applications, check top memory consumers."; }
        else if (pct >= 75) { score = 60; status = "Warning"; reason = $"RAM usage is {pct:0.#}%. Moderate pressure."; rec = "Close unused apps."; status = "Warning"; }
        else { score = 85; status = "Good"; reason = $"RAM usage is {pct:0.#}%. Healthy."; rec = "No action needed."; }
        return new HealthFactorScore { Factor = HealthFactor.Memory, Score = score, Weight = 0.20, Reason = reason, Recommendation = rec, Status = status };
    }

    private static HealthFactorScore ScoreCpu(SystemSnapshot s)
    {
        double c = s.CpuPercent;
        int score = c >= 85 ? 30 : c >= 60 ? 60 : c >= 40 ? 75 : 88;
        string status = score < 50 ? "Critical" : score < 75 ? "Warning" : "Good";
        string reason = $"CPU usage is {c:0.#}% ({s.CpuModel}).";
        string rec = c >= 60 ? "Check Resource Monitor for high CPU processes." : "No action needed.";
        return new HealthFactorScore { Factor = HealthFactor.Cpu, Score = score, Weight = 0.15, Reason = reason, Recommendation = rec, Status = status };
    }

    private static HealthFactorScore ScoreDisk(SystemSnapshot s)
    {
        double d = s.DiskActivePercent;
        int score = d >= 90 ? 35 : d >= 60 ? 60 : 85;
        string status = score < 50 ? "Critical" : score < 75 ? "Warning" : "Good";
        string reason = $"Disk active time is {d:0.#}% ({s.DiskType}).";
        string rec = d >= 60 ? "Check high disk I/O processes." : "No action needed.";
        return new HealthFactorScore { Factor = HealthFactor.Disk, Score = score, Weight = 0.15, Reason = reason, Recommendation = rec, Status = status };
    }

    private static HealthFactorScore ScoreBackground(SystemSnapshot s)
    {
        int highCpu = s.TopCpu.Count(p => p.CpuPercent > 15);
        int highMem = s.TopMemory.Count(p => p.MemoryBytes > 1L * 1024 * 1024 * 1024);
        int score = (highCpu + highMem) >= 3 ? 45 : (highCpu + highMem) >= 1 ? 70 : 90;
        string status = score < 60 ? "Warning" : "Good";
        string reason = highCpu + highMem > 0 ? $"{highCpu} high CPU and {highMem} high memory background processes detected." : "Background load is normal.";
        string rec = score < 80 ? "Review Resource Monitor for heavy processes." : "No action needed.";
        return new HealthFactorScore { Factor = HealthFactor.Background, Score = score, Weight = 0.10, Reason = reason, Recommendation = rec, Status = status };
    }
}
