using System.Runtime.InteropServices;
using Microsoft.Win32;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Commands;

namespace PcCleaner.Infrastructure.SystemTools;

/// <summary>Snapshot of the hibernation-related system state.</summary>
/// <param name="FileType">HiberFileType: 1 = reduced, 2 = full, 0 = not set (full).</param>
public sealed record HibernationInfo(bool Enabled, int FileType, long HiberfilBytes, long RamBytes, bool FastStartupSetting);

/// <summary>
/// Hibernation: Full / Reduced / Off via powercfg. "Reduced" keeps Fast Startup
/// with a hiberfil.sys about half the size, but removes Hibernate from the power menu.
/// </summary>
public class HibernationTweak : ISystemTweak
{
    public const string Full = "full";
    public const string Reduced = "reduced";
    public const string Off = "off";

    // Windows defaults: full hiberfil ≈ 40% of RAM, reduced ≈ 20%
    private const double FullRatio = 0.40;
    private const double ReducedRatio = 0.20;

    private readonly ICommandRunner _runner;
    private readonly Func<HibernationInfo> _readState;

    public HibernationTweak(ICommandRunner runner) : this(runner, ReadSystemState)
    {
    }

    internal HibernationTweak(ICommandRunner runner, Func<HibernationInfo> readState)
    {
        _runner = runner;
        _readState = readState;
    }

    public string Id => "hibernation";
    public string Title => "Hibernation";
    public string Category => "Power & Storage";
    public string IconGlyph => "\uE708";
    public bool RequiresAdmin => true;

    public string Summary =>
        "Hibernate saves everything open in RAM to C:\\hiberfil.sys and powers the PC off completely, " +
        "so you can resume exactly where you left off with zero power use. Windows' Fast Startup uses the " +
        "same file to boot faster after a shutdown. The file takes several GB on C:.";

    public IReadOnlyList<TweakOption> Options { get; } = new[]
    {
        new TweakOption(Full, "Full",
            "Hibernate in the power menu, Fast Startup and Hybrid Sleep all work. Uses the most space. " +
            "Best for laptops you carry around with work left open.",
            "Laptops"),
        new TweakOption(Reduced, "Reduced",
            "Keeps Fast Startup, but Hibernate disappears from the power menu. hiberfil.sys shrinks to about half. " +
            "A good middle ground if you only care about fast boot.",
            "Balanced"),
        new TweakOption(Off, "Off",
            "No Hibernate and no Fast Startup; hiberfil.sys is deleted and all of its space is freed. " +
            "Boot after shutdown may take a few seconds longer (barely noticeable on an SSD). " +
            "Fully reversible - choose Full or Reduced again any time.",
            "Desktops"),
    };

    public Task<TweakState> GetStateAsync()
    {
        var info = _readState();
        return Task.FromResult(BuildState(info));
    }

    internal static TweakState BuildState(HibernationInfo info)
    {
        string current = !info.Enabled ? Off : info.FileType == 1 ? Reduced : Full;

        var hints = new Dictionary<string, string>
        {
            [Full] = $"≈ {Gb(info.RamBytes * FullRatio)} on C:",
            [Reduced] = $"≈ {Gb(info.RamBytes * ReducedRatio)} on C:",
            [Off] = info.HiberfilBytes > 0 ? $"Frees {Gb(info.HiberfilBytes)}" : "0 GB on C:"
        };

        var status = current switch
        {
            Off => "Off",
            Reduced => $"Reduced, {Gb(info.HiberfilBytes)}",
            _ => $"Full, {Gb(info.HiberfilBytes)}"
        };

        var fastStartup = info.Enabled && info.FastStartupSetting ? "on" : "off";
        var hiberfil = info.HiberfilBytes > 0 ? $"hiberfil.sys uses {Gb(info.HiberfilBytes)}" : "hiberfil.sys is not on the drive";
        var details = $"This PC has {Gb(info.RamBytes)} of RAM, {hiberfil}, and Fast Startup is {fastStartup}.";

        return new TweakState(current, status, details, hints);
    }

    public Task<TweakApplyResult> ApplyAsync(string optionId)
    {
        return Task.Run(() =>
        {
            var steps = optionId switch
            {
                Full => new[] { "/hibernate on", "/hibernate /type full" },
                Reduced => new[] { "/hibernate on", "/hibernate /type reduced" },
                Off => new[] { "/hibernate off" },
                _ => throw new ArgumentException($"Unknown option {optionId}", nameof(optionId))
            };

            var powercfg = Path.Combine(Environment.SystemDirectory, "powercfg.exe");
            foreach (var args in steps)
            {
                var result = _runner.Run(powercfg, args, TimeSpan.FromSeconds(60));
                if (!result.Succeeded)
                {
                    var error = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
                    return new TweakApplyResult(false, $"powercfg {args} failed: {error.Trim()}");
                }
            }

            var title = Options.First(o => o.Id == optionId).Title;
            return new TweakApplyResult(true, $"Hibernation set to {title}.");
        });
    }

    private static string Gb(double bytes) => $"{bytes / (1024.0 * 1024 * 1024):0.0} GB";

    private static HibernationInfo ReadSystemState()
    {
        using var power = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power");
        using var sessionPower = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");

        bool enabled = Convert.ToInt32(power?.GetValue("HibernateEnabled") ?? 0) != 0;
        int fileType = Convert.ToInt32(power?.GetValue("HiberFileType") ?? 0);
        bool fastStartup = Convert.ToInt32(sessionPower?.GetValue("HiberbootEnabled") ?? 0) != 0;

        long hiberfil = 0;
        try
        {
            var file = new FileInfo(Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "hiberfil.sys"));
            if (file.Exists)
                hiberfil = file.Length;
        }
        catch
        {
            // not readable
        }

        return new HibernationInfo(enabled, fileType, hiberfil, TotalPhysicalMemory(), fastStartup);
    }

    private static long TotalPhysicalMemory()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (long)status.ullTotalPhys : 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
