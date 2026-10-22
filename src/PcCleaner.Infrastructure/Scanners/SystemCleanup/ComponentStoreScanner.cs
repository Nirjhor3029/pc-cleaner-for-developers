using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.SystemCleanup;

/// <summary>
/// Component Store (WinSxS) cleanup. WinSxS must never be touched directly;
/// superseded components are removed through the servicing stack with
/// <c>DISM /Online /Cleanup-Image /StartComponentCleanup</c>. The reclaimable
/// size can only be measured by a slow DISM analysis, so it is reported as unknown.
/// </summary>
public class ComponentStoreScanner : IScanner
{
    public string Name => "Component Store (WinSxS)";
    public ScannerType Type => ScannerType.ComponentStore;
    public bool RequiresAdmin => true;

    public Task<ScanResult> ScanAsync()
    {
        var result = new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            SelectedByDefault = false,
            SizeUnknown = true,
            Warning = "⚠ Runs DISM - can take 10+ minutes; size known only after cleanup"
        };

        var dism = Path.Combine(Environment.SystemDirectory, "dism.exe");
        if (File.Exists(dism))
        {
            result.Items.Add(new ScanItem
            {
                FilePath = "DISM component cleanup",
                SizeBytes = 0,
                Command = new CleanupCommand(dism, "/Online /Cleanup-Image /StartComponentCleanup")
            });
        }

        return Task.FromResult(result);
    }
}
