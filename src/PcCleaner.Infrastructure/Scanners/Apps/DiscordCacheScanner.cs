using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Apps;

/// <summary>
/// Scans Discord cache under %APPDATA%\Discord (Cache, Code Cache, GPUCache,
/// logs). Account data and files (Local Storage / IndexedDB / sqlite) are
/// preserved.
/// </summary>
public sealed class DiscordCacheScanner : AppCacheScannerBase
{
    public override string Name => "Discord Cache";
    public override ScannerType Type => ScannerType.DiscordCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(roam, "Discord");
        yield return Path.Combine(roam, "discordptb");
        yield return Path.Combine(roam, "discordcanary");
    }

    protected override string[] CacheSubdirs { get; } =
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "logs"
    };
}