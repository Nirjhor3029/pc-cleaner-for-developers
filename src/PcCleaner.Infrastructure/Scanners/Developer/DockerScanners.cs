using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Commands;

namespace PcCleaner.Infrastructure.Scanners.Developer;

/// <summary>
/// Reads reclaimable space from <c>docker system df</c>. Docker data lives in a
/// VM disk image, so it can only be cleaned through the Docker CLI, never by
/// deleting files. Returns nothing if Docker isn't installed or the daemon is stopped.
/// </summary>
internal static class DockerDiskUsage
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(20);
    private static readonly Regex SizePattern = new(@"^\s*([\d.]+)\s*([kKMGTP]?B)", RegexOptions.Compiled);

    /// <summary>Reclaimable bytes keyed by df Type ("Images", "Containers", "Local Volumes", "Build Cache").</summary>
    public static Dictionary<string, long> Query(ICommandRunner runner)
    {
        try
        {
            var outcome = runner.Run("docker", "system df --format \"{{json .}}\"", QueryTimeout);
            return outcome.Succeeded ? Parse(outcome.StandardOutput) : new();
        }
        catch
        {
            // docker not on PATH
            return new();
        }
    }

    internal static Dictionary<string, long> Parse(string output)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("Type", out var type) && root.TryGetProperty("Reclaimable", out var reclaimable))
                    result[type.GetString() ?? string.Empty] = ParseSize(reclaimable.GetString());
            }
            catch (JsonException)
            {
                // ignore non-JSON lines (warnings)
            }
        }
        return result;
    }

    /// <summary>Parses Docker's human sizes ("1.2GB (50%)", "512kB", "0B"); Docker uses decimal units.</summary>
    internal static long ParseSize(string? text)
    {
        var match = SizePattern.Match(text ?? string.Empty);
        if (!match.Success || !double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return 0;

        var multiplier = char.ToUpperInvariant(match.Groups[2].Value[0]) switch
        {
            'K' => 1e3,
            'M' => 1e6,
            'G' => 1e9,
            'T' => 1e12,
            'P' => 1e15,
            _ => 1
        };
        return (long)(value * multiplier);
    }
}

/// <summary>
/// Docker build cache, unused images and stopped containers. All of these can
/// be rebuilt or re-pulled; running containers and their images are kept.
/// </summary>
public class DockerCleanupScanner : IScanner
{
    private readonly ICommandRunner _runner;

    public DockerCleanupScanner(ICommandRunner runner)
    {
        _runner = runner;
    }

    public string Name => "Docker (build cache, images, containers)";
    public ScannerType Type => ScannerType.DockerCleanup;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var usage = DockerDiskUsage.Query(_runner);
        var result = new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Warning = "Images will be re-pulled when needed"
        };

        AddIfReclaimable(result, usage, "Build Cache", "Docker build cache", "builder prune -a -f");
        AddIfReclaimable(result, usage, "Images", "Docker unused images", "image prune -a -f");
        AddIfReclaimable(result, usage, "Containers", "Docker stopped containers", "container prune -f");
        return Task.FromResult(result);
    }

    internal static void AddIfReclaimable(ScanResult result, Dictionary<string, long> usage,
        string dfType, string label, string pruneArgs)
    {
        if (!usage.TryGetValue(dfType, out var bytes) || bytes <= 0)
            return;

        result.Items.Add(new ScanItem
        {
            FilePath = label,
            SizeBytes = bytes,
            Command = new CleanupCommand("docker", pruneArgs)
        });
    }
}

/// <summary>
/// Docker volumes not used by any container. Volumes can hold real application
/// data (databases), so this is off by default with a warning.
/// </summary>
public class DockerVolumesScanner : IScanner
{
    private readonly ICommandRunner _runner;

    public DockerVolumesScanner(ICommandRunner runner)
    {
        _runner = runner;
    }

    public string Name => "Docker Unused Volumes";
    public ScannerType Type => ScannerType.DockerVolumes;
    public bool RequiresAdmin => false;

    public Task<ScanResult> ScanAsync()
    {
        var result = new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            SelectedByDefault = false,
            Warning = "⚠ Volumes may contain app data (e.g. databases) - permanently deleted"
        };

        DockerCleanupScanner.AddIfReclaimable(result, DockerDiskUsage.Query(_runner),
            "Local Volumes", "Docker unused volumes", "volume prune -a -f");
        return Task.FromResult(result);
    }
}
