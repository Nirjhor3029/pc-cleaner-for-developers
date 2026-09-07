using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Scans the Flutter Pub package cache under %LOCALAPPDATA%\Pub\Cache.
/// Packages are re-resolved by <c>flutter pub get</c> on demand.
/// </summary>
public sealed class FlutterPubCacheScanner : DevelopersCacheScannerBase
{
    public override string Name => "Flutter Pub Cache";
    public override ScannerType Type => ScannerType.FlutterPubCache;

    protected override IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Pub", "Cache");
    }
}