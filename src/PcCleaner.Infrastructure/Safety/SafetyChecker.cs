using System.IO;
using System.Security.Cryptography;

namespace PcCleaner.Infrastructure.Safety;

public class SafetyChecker
{
    public bool IsSafeToDelete(string filePath, string allowedBasePath, bool checkContent = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!IsWithinAllowedDirectory(filePath, allowedBasePath))
            return false;

        if (!File.Exists(filePath) && !Directory.Exists(filePath))
            return false;

        try
        {
            var attributes = File.GetAttributes(filePath);
            if ((attributes & FileAttributes.System) != 0)
                return false;
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    public bool IsWithinAllowedDirectory(string filePath, string allowedBasePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(allowedBasePath))
            return false;

        var fullPath = Path.GetFullPath(filePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var allowedFullPath = Path.GetFullPath(allowedBasePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullPath.StartsWith(allowedFullPath, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            return true;
        }
    }

    public bool IsSymbolicLink(string filePath)
    {
        if (!File.Exists(filePath) && !Directory.Exists(filePath))
            return false;

        try
        {
            var attributes = File.GetAttributes(filePath);
            return (attributes & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }
}