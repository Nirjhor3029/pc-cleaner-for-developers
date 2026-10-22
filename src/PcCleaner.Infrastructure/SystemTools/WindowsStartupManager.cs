using System.Diagnostics;
using Microsoft.Win32;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.SystemTools;

/// <summary>A place Windows starts programs from at sign-in, plus its StartupApproved key.</summary>
/// <param name="RunKeyPath">Registry Run key (null for a startup folder source).</param>
/// <param name="FolderPath">Startup folder (null for a registry source).</param>
public sealed record StartupSource(
    StartupLocation Location,
    RegistryKey Root,
    string? RunKeyPath,
    string? FolderPath,
    string ApprovedKeyPath,
    bool RequiresAdmin);

/// <summary>
/// Reads the Run keys and Startup folders and toggles entries through
/// <c>Explorer\StartupApproved</c>, exactly like Task Manager: the first byte of
/// the 12-byte value is even (02/06) when enabled and odd (03) when disabled.
/// Nothing is ever deleted.
/// </summary>
public class WindowsStartupManager : IStartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Run32Key = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedBase = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    private readonly IReadOnlyList<StartupSource> _sources;

    public WindowsStartupManager() : this(DefaultSources())
    {
    }

    internal WindowsStartupManager(IReadOnlyList<StartupSource> sources)
    {
        _sources = sources;
    }

    private static IReadOnlyList<StartupSource> DefaultSources() => new[]
    {
        new StartupSource(StartupLocation.UserRegistry, Registry.CurrentUser, RunKey, null, ApprovedBase + "Run", false),
        new StartupSource(StartupLocation.MachineRegistry, Registry.LocalMachine, RunKey, null, ApprovedBase + "Run", true),
        new StartupSource(StartupLocation.MachineRegistry32, Registry.LocalMachine, Run32Key, null, ApprovedBase + "Run32", true),
        new StartupSource(StartupLocation.UserStartupFolder, Registry.CurrentUser, null,
            Environment.GetFolderPath(Environment.SpecialFolder.Startup), ApprovedBase + "StartupFolder", false),
        new StartupSource(StartupLocation.CommonStartupFolder, Registry.LocalMachine, null,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), ApprovedBase + "StartupFolder", true),
    };

    public Task<List<StartupEntry>> GetEntriesAsync()
    {
        return Task.Run(() =>
        {
            var entries = new List<StartupEntry>();
            foreach (var source in _sources)
            {
                try
                {
                    entries.AddRange(source.RunKeyPath != null ? ReadRunKey(source) : ReadFolder(source));
                }
                catch
                {
                    // source unreadable - skip it
                }
            }
            return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    public void SetEnabled(StartupEntry entry, bool enabled)
    {
        var source = _sources.First(s => s.Location == entry.Location);
        using var key = source.Root.CreateSubKey(source.ApprovedKeyPath, writable: true);
        key.SetValue(entry.Name, ApprovedValue(enabled, DateTime.UtcNow), RegistryValueKind.Binary);
        entry.IsEnabled = enabled;
    }

    internal static byte[] ApprovedValue(bool enabled, DateTime utcNow)
    {
        var value = new byte[12];
        value[0] = enabled ? (byte)0x02 : (byte)0x03;
        if (!enabled)
            BitConverter.GetBytes(utcNow.ToFileTimeUtc()).CopyTo(value, 4); // when it was disabled, as Task Manager does
        return value;
    }

    /// <summary>Missing or malformed values count as enabled, matching Windows.</summary>
    internal static bool IsApproved(object? value) =>
        value is not byte[] bytes || bytes.Length == 0 || (bytes[0] & 1) == 0;

    private static IEnumerable<StartupEntry> ReadRunKey(StartupSource source)
    {
        using var run = source.Root.OpenSubKey(source.RunKeyPath!);
        if (run == null)
            yield break;

        using var approved = source.Root.OpenSubKey(source.ApprovedKeyPath);
        foreach (var name in run.GetValueNames().Where(n => !string.IsNullOrEmpty(n)))
        {
            var command = run.GetValue(name)?.ToString() ?? string.Empty;
            var exe = ExtractExecutable(command);
            yield return new StartupEntry
            {
                Name = name,
                Command = command,
                ExecutablePath = exe,
                Publisher = PublisherOf(exe),
                Description = DescriptionOf(exe),
                Location = source.Location,
                IsEnabled = IsApproved(approved?.GetValue(name)),
                RequiresAdmin = source.RequiresAdmin
            };
        }
    }

    private static IEnumerable<StartupEntry> ReadFolder(StartupSource source)
    {
        if (string.IsNullOrEmpty(source.FolderPath) || !Directory.Exists(source.FolderPath))
            yield break;

        using var approved = source.Root.OpenSubKey(source.ApprovedKeyPath);
        foreach (var file in Directory.GetFiles(source.FolderPath))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                continue;

            yield return new StartupEntry
            {
                Name = name,
                Command = file,
                ExecutablePath = file,
                Publisher = PublisherOf(file),
                Description = DescriptionOf(file),
                Location = source.Location,
                IsEnabled = IsApproved(approved?.GetValue(name)),
                RequiresAdmin = source.RequiresAdmin
            };
        }
    }

    /// <summary>Gets the program path out of a Run command such as <c>"C:\x\app.exe" --min</c> or <c>C:\x\app.exe /s</c>.</summary>
    internal static string? ExtractExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        var expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (expanded.StartsWith('"'))
        {
            var end = expanded.IndexOf('"', 1);
            return end > 1 ? expanded[1..end] : null;
        }

        var exeIndex = expanded.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex > 0)
            return expanded[..(exeIndex + 4)];

        var space = expanded.IndexOf(' ');
        return space > 0 ? expanded[..space] : expanded;
    }

    private static string? PublisherOf(string? path) => VersionField(path, v => v.CompanyName);

    /// <summary>
    /// A short human name for the program: its file description ("Google Chrome"), else its
    /// product name when the description is a sentence, else the exe name ("Postman Agent").
    /// </summary>
    private static string? DescriptionOf(string? path)
    {
        const int maxLength = 40;
        var description = VersionField(path, v => v.FileDescription);
        if (description is { Length: <= maxLength })
            return description;

        var product = VersionField(path, v => v.ProductName);
        if (product is { Length: <= maxLength })
            return product;

        return !string.IsNullOrEmpty(path) && File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(path)
            : null;
    }

    private static string? VersionField(string? path, Func<FileVersionInfo, string?> field)
    {
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return null;
            var value = field(FileVersionInfo.GetVersionInfo(path));
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch
        {
            return null;
        }
    }
}
