using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners;

/// <summary>
/// DirectX and GPU-driver shader caches. Drivers and games rebuild these on
/// demand (first launch after cleaning may stutter briefly). Files held open by
/// a running game are skipped by the cleaner.
/// </summary>
public class ShaderCacheScanner : FileListScannerBase
{
    private static readonly string[] RelativeRoots =
    {
        "D3DSCache",
        @"NVIDIA\DXCache",
        @"NVIDIA\GLCache",
        @"AMD\DxCache",
        @"AMD\DxcCache",
        @"AMD\GLCache",
        @"AMD\VkCache",
        @"Intel\ShaderCache"
    };

    public override string Name => "Shader Cache";
    public override ScannerType Type => ScannerType.ShaderCache;

    protected override IEnumerable<string> Roots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return RelativeRoots.Select(r => Path.Combine(local, r));
    }
}
