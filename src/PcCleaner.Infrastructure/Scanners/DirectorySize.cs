namespace PcCleaner.Infrastructure.Scanners;

internal static class DirectorySize
{
    /// <summary>Total size of all files under <paramref name="path"/>, including Hidden/System files; 0 if missing.</summary>
    public static long Of(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // skip unreadable file
                }
            }
        }
        catch
        {
            // directory became inaccessible
        }
        return total;
    }
}
