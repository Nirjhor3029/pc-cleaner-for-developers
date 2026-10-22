using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Scanners;

/// <summary>
/// Windows Error Reporting archives and queued reports, both machine-wide
/// (ProgramData, needs admin) and per-user. These are diagnostic copies of
/// crash data that have already been sent or will never be.
/// </summary>
public class WindowsErrorReportingScanner : FileListScannerBase
{
    private static readonly string[] SubDirs = { "ReportArchive", "ReportQueue", "Temp" };

    public override string Name => "Windows Error Reporting";
    public override ScannerType Type => ScannerType.WindowsErrorReporting;
    public override bool RequiresAdmin => true;

    protected override IEnumerable<string> Roots()
    {
        var bases = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var b in bases)
            foreach (var sub in SubDirs)
                yield return Path.Combine(b, @"Microsoft\Windows\WER", sub);
    }
}
