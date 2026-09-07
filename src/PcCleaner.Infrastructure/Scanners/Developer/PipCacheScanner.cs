using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the pip download cache under %LOCALAPPDATA%\pip\Cache. Wheels are
/// re-downloaded on demand.
/// </summary>
public sealed class PipCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "pip Cache";
    public override ScannerType Type => ScannerType.PipCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "pip", "Cache");
    }
}