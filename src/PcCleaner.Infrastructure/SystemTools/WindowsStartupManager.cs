using System.Diagnostics;
using Microsoft.Win32;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Infrastructure.SystemTools;

/// <summary>A place Windows starts programs from at sign-in, plus its StartupApproved key.</summary>
/// <param name="RunKeyPath">Registry Run key (null for a startup folder source).</param>
/// <param name="FolderPath">Startup folder (null for a registry source).</param>
public sealed record StartupRegistration(
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

    private readonly IReadOnlyList<StartupRegistration> _sources;

    public WindowsStartupManager() : this(DefaultSources())
    {
    }

    internal WindowsStartupManager(IReadOnlyList<StartupRegistration> sources)
    {
        _sources = sources;
    }

    private static IReadOnlyList<StartupRegistration> DefaultSources() => new[]
    {
        new StartupRegistration(StartupLocation.UserRegistry, Registry.CurrentUser, RunKey, null, ApprovedBase + "Run", false),
        new StartupRegistration(StartupLocation.MachineRegistry, Registry.LocalMachine, RunKey, null, ApprovedBase + "Run", true),
        new StartupRegistration(StartupLocation.MachineRegistry32, Registry.LocalMachine, Run32Key, null, ApprovedBase + "Run32", true),
        new StartupRegistration(StartupLocation.UserStartupFolder, Registry.CurrentUser, null,
            Environment.GetFolderPath(Environment.SpecialFolder.Startup), ApprovedBase + "StartupFolder", false),
        new StartupRegistration(StartupLocation.CommonStartupFolder, Registry.LocalMachine, null,
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), ApprovedBase + "StartupFolder", true),
    };

    public Task<List<StartupEntry>> GetAsync()
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
            return entries.OrderBy(e => DisplayName(e), StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    public Task<bool> DisableAsync(string id) => SetStatusAsync(id, enabled: false);

    public Task<bool> EnableAsync(string id) => SetStatusAsync(id, enabled: true);

    public async Task<string> GetFileLocationAsync(string id)
    {
        var entry = (await GetAsync()).FirstOrDefault(e => e.Id == id);
        return entry?.FilePath ?? string.Empty;
    }

    private async Task<bool> SetStatusAsync(string id, bool enabled)
    {
        var entry = (await GetAsync()).FirstOrDefault(e => e.Id == id);
        if (entry == null || (!enabled && !entry.IsSafeToDisable))
            return false;

        var source = _sources.First(s => s.Location == entry.Registration);
        using var key = source.Root.CreateSubKey(source.ApprovedKeyPath, writable: true);
        key.SetValue(entry.Name, ApprovedValue(enabled, DateTime.UtcNow), RegistryValueKind.Binary);
        return true;
    }

    private static string DisplayName(StartupEntry e) => string.IsNullOrEmpty(e.Description) ? e.Name : e.Description;

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

    private static IEnumerable<StartupEntry> ReadRunKey(StartupRegistration source)
    {
        using var run = source.Root.OpenSubKey(source.RunKeyPath!);
        if (run == null)
            yield break;

        using var approved = source.Root.OpenSubKey(source.ApprovedKeyPath);
        foreach (var name in run.GetValueNames().Where(n => !string.IsNullOrEmpty(n)))
        {
            var command = run.GetValue(name)?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(command))
                continue;
            yield return CreateEntry(source, name, command, ExtractExecutable(command),
                $@"{source.Root.Name}\{source.RunKeyPath}", IsApproved(approved?.GetValue(name)));
        }
    }

    private static IEnumerable<StartupEntry> ReadFolder(StartupRegistration source)
    {
        if (string.IsNullOrEmpty(source.FolderPath) || !Directory.Exists(source.FolderPath))
            yield break;

        using var approved = source.Root.OpenSubKey(source.ApprovedKeyPath);
        foreach (var file in Directory.GetFiles(source.FolderPath))
        {
            var name = Path.GetFileName(file);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                continue;
            yield return CreateEntry(source, name, file, file, source.FolderPath, IsApproved(approved?.GetValue(name)));
        }
    }

    private static StartupEntry CreateEntry(StartupRegistration source, string name, string command, string? exe,
        string location, bool enabled)
    {
        var isFolder = source.FolderPath != null;
        return new StartupEntry
        {
            Id = $"{source.Location}|{name}",
            Name = name,
            Command = command,
            FilePath = exe ?? string.Empty,
            Publisher = PublisherOf(exe) ?? string.Empty,
            Description = DescriptionOf(exe) ?? string.Empty,
            Source = isFolder
                ? (source.Location == StartupLocation.CommonStartupFolder ? StartupSource.StartupFolderCommon : StartupSource.StartupFolder)
                : StartupSource.RegistryRun,
            Registration = source.Location,
            Location = location,
            Status = enabled ? StartupStatus.Enabled : StartupStatus.Disabled,
            Impact = EstimateImpact(name, exe),
            IsSafeToDisable = !IsCritical(name, exe),
            RequiresAdmin = source.RequiresAdmin
        };
    }

    // Rough guess until real boot-time measurement exists: chat/sync/browser helpers load heavy runtimes
    private static readonly string[] HighImpact = { "discord", "spotify", "teams", "onedrive", "adobe", "chrome", "slack", "docker", "steam", "zoom" };
    private static readonly string[] LowImpact = { "securityhealth", "windows defender", "rtkaud", "realtek" };
    private static readonly string[] Critical = { "securityhealth", "windows defender", "windows security" };

    internal static StartupImpact EstimateImpact(string name, string? exe)
    {
        var text = (name + " " + exe).ToLowerInvariant();
        if (LowImpact.Any(text.Contains))
            return StartupImpact.Low;
        return HighImpact.Any(text.Contains) ? StartupImpact.High : StartupImpact.Medium;
    }

    /// <summary>Security software keeps starting; the UI won't offer to turn it off.</summary>
    internal static bool IsCritical(string name, string? exe)
    {
        var text = (name + " " + exe).ToLowerInvariant();
        return Critical.Any(text.Contains);
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
