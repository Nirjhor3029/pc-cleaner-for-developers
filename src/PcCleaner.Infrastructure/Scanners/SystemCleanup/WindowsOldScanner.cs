using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.SystemCleanup;

/// <summary>
/// The previous Windows installation (Windows.old) left after a feature
/// update. Removing it disables "Go back to the previous version", so it is
/// unchecked by default with a warning.
/// </summary>
public class WindowsOldScanner : DiskCleanupScannerBase
{
    public override string Name => "Old Windows Installation";
    public override ScannerType Type => ScannerType.WindowsOld;

    protected override void Configure(ScanResult result)
    {
        result.SelectedByDefault = false;
        result.Warning = "⚠ Removes the option to roll back to your previous Windows version";
    }

    protected override IEnumerable<DiskCleanupHandler> Handlers()
    {
        yield return new DiskCleanupHandler("Windows.old (previous installation)", "Previous Installations", 4201,
            new[] { Path.Combine(SystemDrive, "Windows.old") });
    }
}
