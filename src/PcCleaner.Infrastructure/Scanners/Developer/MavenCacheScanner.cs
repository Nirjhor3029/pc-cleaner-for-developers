using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the Maven wrapper distribution cache under %USERPROFILE%\.m2\wrapper.
/// The local repository itself (%USERPROFILE%\.m2\repository) is deliberately
/// excluded — it is the library store, not a cache.
/// </summary>
public sealed class MavenCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "Maven Wrapper Cache";
    public override ScannerType Type => ScannerType.MavenCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(root, ".m2", "wrapper");
    }
}