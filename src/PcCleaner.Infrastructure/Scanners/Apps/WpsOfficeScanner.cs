using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Apps;

/// <summary>
/// WPS Office (Kingsoft) keeps every previous version folder after an
/// auto-update (~1.6 GB each), plus downloaded update installers, cache and
/// logs. The newest version folder and any version a running WPS process was
/// started from are always kept; add-ons and documents are never touched.
/// </summary>
public class WpsOfficeScanner : IScanner
{
    private static readonly string[] JunkSubDirs = { @"office6\updatenew", @"office6\cache", @"office6\log" };

    public string Name => "WPS Office (old versions, updates, cache)";
    public ScannerType Type => ScannerType.WpsOffice;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var installRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Kingsoft\WPS Office");
        var dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "kingsoft");
        return Task.FromResult(Scan(installRoot, dataRoot, RunningProcessPaths()));
    }

    internal ScanResult Scan(string installRoot, string dataRoot, IReadOnlyCollection<string> runningPaths)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };

        foreach (var dir in OldVersionDirs(installRoot))
        {
            var prefix = dir.TrimEnd('\\') + '\\';
            if (runningPaths.Any(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Items.Add(new ScanItem { FilePath = dir, SizeBytes = DirectorySize.Of(dir) });
        }

        foreach (var sub in JunkSubDirs)
        {
            var path = Path.Combine(dataRoot, sub);
            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint }))
            {
                try
                {
                    result.Items.Add(new ScanItem { FilePath = file, SizeBytes = new FileInfo(file).Length });
                }
                catch
                {
                    // skip unreadable file
                }
            }
        }

        return result;
    }

    /// <summary>Version folders (e.g. 12.1.0.28485) except the newest one.</summary>
    internal static List<string> OldVersionDirs(string installRoot)
    {
        if (!Directory.Exists(installRoot))
            return new();

        var versions = Directory.GetDirectories(installRoot)
            .Select(d => (Dir: d, Ok: Version.TryParse(Path.GetFileName(d), out var v), Version: v))
            .Where(x => x.Ok && x.Version != null)
            .OrderByDescending(x => x.Version)
            .ToList();

        return versions.Skip(1).Select(x => x.Dir).ToList();
    }

    private static List<string> RunningProcessPaths()
    {
        var paths = new List<string>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                    paths.Add(ToLongPath(path));
            }
            catch
            {
                // access denied (elevated/system process) - not a per-user WPS process
            }
            finally
            {
                process.Dispose();
            }
        }
        return paths;
    }

    /// <summary>Expands 8.3 short names (WPSOFF~1\1210~1.291) so prefix checks are reliable.</summary>
    private static string ToLongPath(string path)
    {
        var buffer = new StringBuilder(1024);
        var length = GetLongPathName(path, buffer, buffer.Capacity);
        return length > 0 && length < buffer.Capacity ? buffer.ToString() : path;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetLongPathName(string shortPath, StringBuilder longPath, int bufferLength);
}
