using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans and cleans the Yarn cache directory (both classic and Berry layouts).
/// Only the package-manager cache is affected.
/// </summary>
public class YarnCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "Yarn Cache";
    public override ScannerType Type => ScannerType.YarnCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Yarn", "Cache");
        yield return Path.Combine(local, "Yarn", "Berry", "cache");
    }
}