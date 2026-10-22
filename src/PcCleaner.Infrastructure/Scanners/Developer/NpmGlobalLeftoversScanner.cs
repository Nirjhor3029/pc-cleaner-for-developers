using System.Text.RegularExpressions;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// When npm updates a global package it first renames the old copy to
/// <c>.&lt;name&gt;-&lt;8 random chars&gt;</c> and deletes it afterwards. If the
/// update is interrupted (or the package was running), that folder stays
/// behind forever. Only these leftovers are cleaned; installed packages are never touched.
/// </summary>
public class NpmGlobalLeftoversScanner : IScanner
{
    private static readonly Regex LeftoverName = new(@"^\.[^.].*-[A-Za-z0-9]{8}$", RegexOptions.Compiled);

    public string Name => "npm Global Update Leftovers";
    public ScannerType Type => ScannerType.NpmGlobalLeftovers;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\node_modules");
        return Task.FromResult(ScanNodeModules(root));
    }

    internal ScanResult ScanNodeModules(string nodeModules)
    {
        var result = new ScanResult { ScannerName = Name, Type = Type };
        if (!Directory.Exists(nodeModules))
            return result;

        // Leftovers live next to the package: node_modules\.pkg-XXXX or node_modules\@scope\.pkg-XXXX
        var parents = new List<string> { nodeModules };
        parents.AddRange(SafeDirectories(nodeModules).Where(d => Path.GetFileName(d).StartsWith('@')));

        foreach (var parent in parents)
        {
            foreach (var dir in SafeDirectories(parent))
            {
                if (!LeftoverName.IsMatch(Path.GetFileName(dir)))
                    continue;

                result.Items.Add(new ScanItem
                {
                    FilePath = dir,
                    SizeBytes = DirectorySize.Of(dir)
                });
            }
        }

        return result;
    }

    private static IEnumerable<string> SafeDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            });
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
