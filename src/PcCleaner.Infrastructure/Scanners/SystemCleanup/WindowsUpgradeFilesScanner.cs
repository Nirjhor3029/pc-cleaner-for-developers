using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners.SystemCleanup;

/// <summary>
/// Leftover Windows upgrade staging folders ($Windows.~BT, $Windows.~WS) and
/// ESD installation files. cleanmgr keeps them if an upgrade is still pending.
/// </summary>
public class WindowsUpgradeFilesScanner : DiskCleanupScannerBase
{
    public override string Name => "Windows Upgrade Files";
    public override ScannerType Type => ScannerType.WindowsUpgradeFiles;

    protected override IEnumerable<DiskCleanupHandler> Handlers()
    {
        yield return new DiskCleanupHandler("Temporary Windows setup files", "Temporary Setup Files", 4202,
            new[] { Path.Combine(SystemDrive, "$Windows.~BT"), Path.Combine(SystemDrive, "$Windows.~WS") });

        yield return new DiskCleanupHandler("Windows ESD installation files", "Windows ESD installation files", 4203,
            new[] { Path.Combine(SystemDrive, @"ESD\Windows") });
    }
}
