using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.SystemCleanup;

/// <summary>
/// hiberfil.sys can't be deleted directly; Windows removes it when hibernation
/// is turned off with <c>powercfg /hibernate off</c>. That also disables Fast
/// Startup, so it's off by default and reversible with <c>powercfg /hibernate on</c>.
/// </summary>
public class HibernationScanner : IScanner
{
    public string Name => "Turn Off Hibernation (hiberfil.sys)";
    public ScannerType Type => ScannerType.Hibernation;
    public bool RequiresAdmin => true;

    public Task<ScanResult> ScanAsync()
    {
        var drive = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        return Task.FromResult(ScanFile(Path.Combine(drive, "hiberfil.sys")));
    }

    internal ScanResult ScanFile(string hiberfilPath)
    {
        var result = new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            SelectedByDefault = false,
            Warning = "⚠ Disables Hibernate & Fast Startup (undo: powercfg /hibernate on)"
        };

        long size = 0;
        try
        {
            var info = new FileInfo(hiberfilPath);
            if (info.Exists)
                size = info.Length;
        }
        catch
        {
            // hiberfil.sys not readable
        }

        if (size > 0)
        {
            result.Items.Add(new ScanItem
            {
                FilePath = "Disable hibernation (powercfg /hibernate off)",
                SizeBytes = size,
                Command = new CleanupCommand(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), "/hibernate off")
            });
        }

        return result;
    }
}
