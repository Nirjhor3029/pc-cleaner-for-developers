using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the NuGet HTTP/packages cache under %LOCALAPPDATA%\NuGet\v3-cache
/// (and v2). These files are re-downloaded on demand. The global packages
/// folder (%USERPROFILE%\.nuget\packages) is intentionally excluded — it is a
/// library store, not a cache.
/// </summary>
public sealed class NuGetCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "NuGet HTTP Cache";
    public override ScannerType Type => ScannerType.NuGetCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "NuGet", "v3-cache");
        yield return Path.Combine(local, "NuGet", "v2-cache");
    }
}