using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public sealed class StartupEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string Command { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public StartupImpact Impact { get; init; } = StartupImpact.NotMeasured;
    public StartupStatus Status { get; set; } = StartupStatus.Enabled;
    public StartupSource Source { get; init; }
    public string Location { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public bool IsSafeToDisable { get; init; } = true;
    public string DisableBackupKey { get; init; } = string.Empty;
}
