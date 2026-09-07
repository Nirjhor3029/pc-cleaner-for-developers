using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the pnpm cache/store under %LOCALAPPDATA%\pnpm. Archived packages are
/// re-fetched from the registry on demand.
/// </summary>
public sealed class PnpmCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "pnpm Cache";
    public override ScannerType Type => ScannerType.PnpmCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "pnpm-cache");
        yield return Path.Combine(local, "pnpm");
    }
}