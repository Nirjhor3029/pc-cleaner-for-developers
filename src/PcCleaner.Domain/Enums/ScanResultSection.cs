namespace PcCleaner.Domain.Enums;

public enum CleanupSection
{
    System,
    Developer,
    Apps
}

public static class ScannerTypeExtensions
{
    public static CleanupSection Section(this ScannerType type)
    {
        return type switch
        {
            ScannerType.NpmCache
                or ScannerType.YarnCache
                or ScannerType.ComposerCache
                or ScannerType.VSCodeCache
                or ScannerType.PlaywrightCache
                or ScannerType.GradleCache
                or ScannerType.AndroidStudioCache
                or ScannerType.NuGetCache
                or ScannerType.HuggingFaceCache
                or ScannerType.PnpmCache
                or ScannerType.BunCache
                or ScannerType.MavenCache
                or ScannerType.PipCache
                or ScannerType.FlutterPubCache => CleanupSection.Developer,
            ScannerType.TeamsCache
                or ScannerType.SlackCache
                or ScannerType.DiscordCache => CleanupSection.Apps,
            _ => CleanupSection.System
        };
    }
}