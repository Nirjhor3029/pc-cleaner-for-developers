using System.Diagnostics;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Docker Desktop stores everything in a WSL virtual disk (.vhdx) that grows
/// but never shrinks, so space freed by <c>docker prune</c> stays allocated on
/// C:. Compacting the disk with diskpart returns it. Requires Docker Desktop to
/// be closed and shuts down WSL, so it's off by default and needs admin.
/// </summary>
public class DockerDiskCompactScanner : IScanner
{
    private static readonly string[] DockerProcesses = { "Docker Desktop", "com.docker.backend", "com.docker.build" };

    public string Name => "Compact Docker Disk (WSL .vhdx)";
    public ScannerType Type => ScannerType.DockerDiskCompact;
    public bool RequiresAdmin => true;

    public Task<ScanResult> ScanAsync()
    {
        var dockerRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Docker\wsl");
        var scriptDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PC-Cleaner");
        bool dockerRunning = DockerProcesses.Any(n => Process.GetProcessesByName(n).Length > 0);
        return Task.FromResult(Scan(dockerRoot, scriptDir, dockerRunning));
    }

    internal ScanResult Scan(string dockerWslRoot, string scriptDir, bool dockerRunning)
    {
        var result = new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            SelectedByDefault = false,
            SizeUnknown = true,
            Warning = "⚠ Close Docker Desktop first; shuts down WSL. Run Docker prune before this"
        };

        var vhdx = new[] { @"disk\docker_data.vhdx", @"data\ext4.vhdx" }
            .Select(p => Path.Combine(dockerWslRoot, p))
            .FirstOrDefault(File.Exists);
        if (vhdx == null)
            return result;

        string script;
        try
        {
            Directory.CreateDirectory(scriptDir);
            script = Path.Combine(scriptDir, "compact-docker-vhdx.txt");
            File.WriteAllLines(script, BuildDiskpartScript(vhdx));
        }
        catch
        {
            return result;
        }

        result.Items.Add(new ScanItem
        {
            FilePath = $"Compact {vhdx}",
            SizeBytes = 0,
            IsDeletable = !dockerRunning,
            Command = new CleanupCommand("cmd.exe", $"/c wsl.exe --shutdown && diskpart.exe /s \"{script}\"")
        });
        return result;
    }

    internal static string[] BuildDiskpartScript(string vhdx) => new[]
    {
        $"select vdisk file=\"{vhdx}\"",
        "attach vdisk readonly",
        "compact vdisk",
        "detach vdisk"
    };
}
