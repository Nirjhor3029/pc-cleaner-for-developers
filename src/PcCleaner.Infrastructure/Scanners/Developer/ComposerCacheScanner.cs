using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans and cleans the Composer (PHP) cache directory. Only the Composer
/// cache is affected.
/// </summary>
public class ComposerCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "Composer Cache";
    public override ScannerType Type => ScannerType.ComposerCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(roaming, "Composer", "cache");
    }
}