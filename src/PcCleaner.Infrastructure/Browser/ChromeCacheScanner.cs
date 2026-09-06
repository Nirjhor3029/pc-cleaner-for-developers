using System.Diagnostics;
using PcCleaner.Domain.Enums;

namespace PcCleaner.Infrastructure.Browser;

public class ChromeCacheScanner : BrowserCacheScannerBase
{
    public override string Name => "Chrome Cache";
    public override ScannerType Type => ScannerType.ChromeCache;

    protected override string BrowserUserDataRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Google\Chrome\User Data");
    }

    protected override bool IsBrowserRunning() => Process.GetProcessesByName("chrome").Length > 0;
}