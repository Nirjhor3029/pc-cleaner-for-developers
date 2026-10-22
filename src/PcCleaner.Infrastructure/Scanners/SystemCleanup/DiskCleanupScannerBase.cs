using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.SystemCleanup;

/// <summary>
/// One Windows Disk Cleanup (cleanmgr) handler and the folders it removes.
/// Each handler gets its own sageset number so enabling one never makes
/// <c>cleanmgr /sagerun</c> clean another category the user didn't select.
/// </summary>
public sealed record DiskCleanupHandler(string Label, string RegistryKey, int SageSetId, string[] Paths);

/// <summary>
/// Base for categories owned by TrustedInstaller (Windows.old, $Windows.~BT, …)
/// that can't be deleted file-by-file. Sizes are measured directly; cleaning is
/// delegated to the built-in cleanmgr handler, which also knows when files are
/// still needed (e.g. an upgrade in progress).
/// </summary>
public abstract class DiskCleanupScannerBase : IScanner
{
    private const string VolumeCachesKey = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches";

    public abstract string Name { get; }
    public abstract ScannerType Type { get; }
    public bool RequiresAdmin => true;

    protected abstract IEnumerable<DiskCleanupHandler> Handlers();

    protected virtual void Configure(ScanResult result)
    {
    }

    protected static string SystemDrive => Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";

    public Task<ScanResult> ScanAsync()
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        Configure(result);

        foreach (var handler in Handlers())
        {
            var size = handler.Paths.Sum(DirectorySize.Of);
            if (size <= 0)
                continue;

            result.Items.Add(new ScanItem
            {
                FilePath = handler.Label,
                SizeBytes = size,
                Command = BuildCommand(handler)
            });
        }

        return Task.FromResult(result);
    }

    internal static CleanupCommand BuildCommand(DiskCleanupHandler handler)
    {
        var flag = $"StateFlags{handler.SageSetId:D4}";
        return new CleanupCommand("cmd.exe",
            $"/c reg add \"{VolumeCachesKey}\\{handler.RegistryKey}\" /v {flag} /t REG_DWORD /d 2 /f " +
            $"&& cleanmgr.exe /sagerun:{handler.SageSetId}");
    }
}
