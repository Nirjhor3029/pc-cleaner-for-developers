using PcCleaner.Domain.Enums;

namespace PcCleaner.Domain.Models;

public sealed class StartupEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Registry value name or startup-folder file name, exactly as Windows stores it.</summary>
    public string Name { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;

    /// <summary>Friendly program name from the executable (e.g. "Google Chrome"), if it has one.</summary>
    public string Description { get; init; } = string.Empty;

    public StartupImpact Impact { get; init; } = StartupImpact.NotMeasured;
    public StartupStatus Status { get; set; } = StartupStatus.Enabled;
    public StartupSource Source { get; init; }

    /// <summary>Exact Run key / Startup folder the entry comes from.</summary>
    public StartupLocation Registration { get; init; }

    /// <summary>Human-readable location, e.g. a registry path or folder.</summary>
    public string Location { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    /// <summary>False for security software that should keep starting (e.g. Windows Security).</summary>
    public bool IsSafeToDisable { get; init; } = true;

    /// <summary>Machine-wide entries can only be changed as administrator.</summary>
    public bool RequiresAdmin { get; init; }

    public string DisableBackupKey { get; init; } = string.Empty;
}
