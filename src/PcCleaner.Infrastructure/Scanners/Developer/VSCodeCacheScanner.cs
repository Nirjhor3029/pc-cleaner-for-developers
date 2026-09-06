using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Cleans only VS Code cache/log directories under %APPDATA%\Code. Extensions,
/// settings, and project data are never touched.
/// </summary>
public class VSCodeCacheScanner : IScanner
{
    private static readonly string[] CacheSubDirs =
    {
        "Cache",
        "CachedData",
        "Code Cache",
        "GPUCache",
        "logs"
    };

    public string Name => "VS Code Cache";
    public ScannerType Type => ScannerType.VSCodeCache;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var codeDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Code");
        return ScanDirectoryAsync(codeDir);
    }

    internal Task<ScanResult> ScanDirectoryAsync(string baseDir)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (!Directory.Exists(baseDir))
            return Task.FromResult(result);

        return Task.Run(() =>
        {
            try
            {
                // cache/log dirs sit at the root of the Code install dir
                AddSubDirs(baseDir, result);

                // and under workspaceStorage (Cache/Code Cache can live here too)
                var wsRoot = Path.Combine(baseDir, "User", "workspaceStorage");
                if (Directory.Exists(wsRoot))
                {
                    foreach (var ws in Directory.EnumerateDirectories(wsRoot))
                        AddSubDirs(ws, result);
                }
            }
            catch
            {
                // ignore enumeration errors
            }
            return result;
        });
    }

    private static void AddSubDirs(string baseDir, ScanResult result)
    {
        if (!Directory.Exists(baseDir))
            return;

        foreach (var cacheDir in CacheSubDirs)
        {
            var dir = Path.Combine(baseDir, cacheDir);
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in SafeEnumerateFiles(dir))
            {
                try
                {
                    var info = new FileInfo(file);
                    result.Items.Add(new ScanItem
                    {
                        FilePath = info.FullName,
                        SizeBytes = info.Length,
                        IsDeletable = true
                    });
                }
                catch
                {
                    // skip unreadable file
                }
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path)
    {
        try
        {
            return Directory.EnumerateFiles(path, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true
            });
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}