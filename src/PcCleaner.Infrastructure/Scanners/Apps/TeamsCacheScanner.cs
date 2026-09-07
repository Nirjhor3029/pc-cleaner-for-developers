using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Apps;

/// <summary>
/// Scans classic Microsoft Teams cache under %LOCALAPPDATA%\Microsoft\Teams
/// (Cache, Code Cache, GPUCache, blob_storage, logs, Crashpad). Chats, files
/// and account data (Local Storage / IndexedDB / sqlite) are preserved.
/// </summary>
public sealed class TeamsCacheScanner : AppCacheScannerBase
{
    public override string Name => "Microsoft Teams Cache";
    public override ScannerType Type => ScannerType.TeamsCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Microsoft", "Teams");
        yield return Path.Combine(local, "Packages", "MicrosoftTeams_8wekyb3d8bbwe", "LocalCache", "Local", "Microsoft", "Teams");
    }

    protected override string[] CacheSubdirs { get; } =
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "blob_storage",
        "logs",
        "Crashpad"
    };
}