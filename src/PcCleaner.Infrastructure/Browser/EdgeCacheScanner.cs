using System.Diagnostics;
using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Browser;

public class EdgeCacheScanner : BrowserCacheScannerBase
{
    public override string Name => "Edge Cache";
    public override ScannerType Type => ScannerType.EdgeCache;

    protected override string BrowserUserDataRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\Edge\User Data");
    }

    protected override bool IsBrowserRunning() => Process.GetProcessesByName("msedge").Length > 0;
}