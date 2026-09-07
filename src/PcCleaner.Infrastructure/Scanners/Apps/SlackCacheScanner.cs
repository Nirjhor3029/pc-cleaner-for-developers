using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Apps;

/// <summary>
/// Scans Slack cache under %APPDATA%\Slack (Cache, Code Cache, GPUCache, logs,
/// Service Worker). Account data and files (Local Storage / IndexedDB / sqlite)
/// are preserved.
/// </summary>
public sealed class SlackCacheScanner : AppCacheScannerBase
{
    public override string Name => "Slack Cache";
    public override ScannerType Type => ScannerType.SlackCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(roam, "Slack");
        yield return Path.Combine(roam, "SlackApp");
    }

    protected override string[] CacheSubdirs { get; } =
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "logs",
        "Service Worker"
    };
}