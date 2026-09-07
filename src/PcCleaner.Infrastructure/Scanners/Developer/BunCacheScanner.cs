using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the Bun install cache under %USERPROFILE%\.bun\install\cache.
/// Packages are re-fetched on demand.
/// </summary>
public sealed class BunCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "Bun Cache";
    public override ScannerType Type => ScannerType.BunCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(root, ".bun", "install", "cache");
    }
}