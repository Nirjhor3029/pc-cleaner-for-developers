namespace PcCleaner.Domain.Enums;

public enum CleanupSection
{
    System,
    Developer
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
                or ScannerType.PlaywrightCache => CleanupSection.Developer,
            _ => CleanupSection.System
        };
    }
}