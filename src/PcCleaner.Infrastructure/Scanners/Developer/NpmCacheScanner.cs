using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans and cleans only the npm cache directory. No project's node_modules
/// is ever touched.
/// </summary>
public class NpmCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "npm Cache";
    public override ScannerType Type => ScannerType.NpmCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "npm-cache");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm-cache");
    }
}