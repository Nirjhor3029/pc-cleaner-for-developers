using Microsoft.Win32;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Commands;
using PcCleaner.Infrastructure.SystemTools;

namespace PcCleaner.UnitTests;

public class HibernationTweakTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(false, 0, HibernationTweak.Off)]
    [InlineData(true, 0, HibernationTweak.Full)]
    [InlineData(true, 2, HibernationTweak.Full)]
    [InlineData(true, 1, HibernationTweak.Reduced)]
    public void State_MapsRegistryValuesToOption(bool enabled, int fileType, string expected)
    {
        var state = HibernationTweak.BuildState(new HibernationInfo(enabled, fileType, 0, 16 * Gb, true));
        Assert.Equal(expected, state.CurrentOptionId);
    }

    [Fact]
    public void State_EstimatesSizesFromRam()
    {
        var state = HibernationTweak.BuildState(new HibernationInfo(true, 2, (long)(6.4 * Gb), 16 * Gb, true));

        Assert.Equal("≈ 6.4 GB on C:", state.OptionHints![HibernationTweak.Full]);
        Assert.Equal("≈ 3.2 GB on C:", state.OptionHints[HibernationTweak.Reduced]);
        Assert.Equal("Frees 6.4 GB", state.OptionHints[HibernationTweak.Off]);
        Assert.Contains("Fast Startup is on", state.Details);
    }

    [Theory]
    [InlineData(HibernationTweak.Off, new[] { "/hibernate off" })]
    [InlineData(HibernationTweak.Reduced, new[] { "/hibernate on", "/hibernate /type reduced" })]
    [InlineData(HibernationTweak.Full, new[] { "/hibernate on", "/hibernate /type full" })]
    public async Task Apply_RunsMatchingPowercfgCommands(string option, string[] expectedArgs)
    {
        var runner = new RecordingRunner(new CommandResult(0, "", ""));
        var tweak = new HibernationTweak(runner, () => new HibernationInfo(true, 2, 0, 16 * Gb, true));

        var result = await tweak.ApplyAsync(option);

        Assert.True(result.Success);
        Assert.Equal(expectedArgs, runner.Calls.Select(c => c.Args).ToArray());
        Assert.All(runner.Calls, c => Assert.EndsWith("powercfg.exe", c.File));
    }

    [Fact]
    public async Task Apply_StopsAndReportsOnFailure()
    {
        var runner = new RecordingRunner(new CommandResult(1, "", "Access is denied."));
        var tweak = new HibernationTweak(runner, () => new HibernationInfo(false, 0, 0, 16 * Gb, false));

        var result = await tweak.ApplyAsync(HibernationTweak.Reduced);

        Assert.False(result.Success);
        Assert.Contains("Access is denied", result.Message);
        Assert.Single(runner.Calls);
    }

    private sealed class RecordingRunner : ICommandRunner
    {
        private readonly CommandResult _result;
        public RecordingRunner(CommandResult result) => _result = result;
        public List<(string File, string Args)> Calls { get; } = new();

        public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
        {
            Calls.Add((fileName, arguments));
            return _result;
        }
    }
}

public class WindowsStartupManagerTests : IDisposable
{
    private readonly string _keyRoot = @"Software\PcCleanerTests\" + Guid.NewGuid().ToString("N");
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "PcCleaner-Startup-Tests-" + Guid.NewGuid().ToString("N"));
    private readonly WindowsStartupManager _manager;

    public WindowsStartupManagerTests()
    {
        Directory.CreateDirectory(_folder);
        _manager = new WindowsStartupManager(new[]
        {
            new StartupRegistration(StartupLocation.UserRegistry, Registry.CurrentUser, _keyRoot + @"\Run", null, _keyRoot + @"\Approved\Run", false),
            new StartupRegistration(StartupLocation.UserStartupFolder, Registry.CurrentUser, null, _folder, _keyRoot + @"\Approved\StartupFolder", false),
        });
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyRoot, throwOnMissingSubKey: false);
        Directory.Delete(_folder, true);
    }

    private void AddRun(string name, string command)
    {
        using var run = Registry.CurrentUser.CreateSubKey(_keyRoot + @"\Run");
        run.SetValue(name, command);
    }

    [Fact]
    public async Task ReadsRunKeyAndFolder_WithApprovedState()
    {
        AddRun("Enabled App", @"""C:\Apps\a.exe"" --min");
        AddRun("Disabled App", @"C:\Apps\b.exe /background");
        using (var approved = Registry.CurrentUser.CreateSubKey(_keyRoot + @"\Approved\Run"))
            approved.SetValue("Disabled App", new byte[] { 3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8 }, RegistryValueKind.Binary);
        File.WriteAllText(Path.Combine(_folder, "Tool.lnk"), "x");
        File.WriteAllText(Path.Combine(_folder, "desktop.ini"), "x");

        var entries = await _manager.GetAsync();

        Assert.Equal(3, entries.Count);
        Assert.Equal(StartupStatus.Enabled, entries.Single(e => e.Name == "Enabled App").Status);
        Assert.Equal(StartupStatus.Disabled, entries.Single(e => e.Name == "Disabled App").Status);
        Assert.Equal(@"C:\Apps\a.exe", entries.Single(e => e.Name == "Enabled App").FilePath);
        var folderEntry = entries.Single(e => e.Name == "Tool.lnk");
        Assert.Equal(StartupLocation.UserStartupFolder, folderEntry.Registration);
        Assert.Equal(PcCleaner.Domain.Enums.StartupSource.StartupFolder, folderEntry.Source);
    }

    [Fact]
    public async Task DisableAndEnable_WriteTaskManagerFlag_AndNeverDeleteEntry()
    {
        AddRun("App", @"C:\Apps\a.exe");
        var id = (await _manager.GetAsync()).Single().Id;

        Assert.True(await _manager.DisableAsync(id));
        using (var approved = Registry.CurrentUser.OpenSubKey(_keyRoot + @"\Approved\Run"))
            Assert.Equal(3, ((byte[])approved!.GetValue("App")!)[0]);
        Assert.Equal(StartupStatus.Disabled, (await _manager.GetAsync()).Single().Status);

        Assert.True(await _manager.EnableAsync(id));
        Assert.Equal(StartupStatus.Enabled, (await _manager.GetAsync()).Single().Status);
        using (var run = Registry.CurrentUser.OpenSubKey(_keyRoot + @"\Run"))
            Assert.Equal(@"C:\Apps\a.exe", run!.GetValue("App"));
    }

    [Fact]
    public async Task Enable_TargetsOnlyTheRequestedEntry()
    {
        AddRun("First", @"C:\Apps\first.exe");
        AddRun("Second", @"C:\Apps\second.exe");
        var entries = await _manager.GetAsync();
        foreach (var e in entries)
            await _manager.DisableAsync(e.Id);

        await _manager.EnableAsync(entries.Single(e => e.Name == "Second").Id);

        var after = await _manager.GetAsync();
        Assert.Equal(StartupStatus.Disabled, after.Single(e => e.Name == "First").Status);
        Assert.Equal(StartupStatus.Enabled, after.Single(e => e.Name == "Second").Status);
    }

    [Fact]
    public async Task SecuritySoftware_CannotBeDisabled()
    {
        AddRun("SecurityHealth", @"C:\Windows\System32\SecurityHealthSystray.exe");
        var entry = (await _manager.GetAsync()).Single();

        Assert.False(entry.IsSafeToDisable);
        Assert.False(await _manager.DisableAsync(entry.Id));
        Assert.Equal(StartupStatus.Enabled, (await _manager.GetAsync()).Single().Status);
    }

    [Fact]
    public async Task UnknownId_ReturnsFalse()
    {
        Assert.False(await _manager.DisableAsync("UserRegistry|does-not-exist"));
    }

    [Theory]
    [InlineData("Discord", @"C:\x\Update.exe", StartupImpact.High)]
    [InlineData("SecurityHealth", "", StartupImpact.Low)]
    [InlineData("Everything", @"D:\Everything.exe", StartupImpact.Medium)]
    public void EstimateImpact_UsesAppKind(string name, string exe, StartupImpact expected)
    {
        Assert.Equal(expected, WindowsStartupManager.EstimateImpact(name, exe));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[] { 2, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 6, 0, 0, 0 }, true)]
    [InlineData(new byte[] { 3, 0, 0, 0 }, false)]
    public void IsApproved_FollowsLowBit(byte[]? value, bool expected)
    {
        Assert.Equal(expected, WindowsStartupManager.IsApproved(value));
    }

    [Theory]
    [InlineData(@"""C:\Program Files\App\app.exe"" --tray", @"C:\Program Files\App\app.exe")]
    [InlineData(@"C:\Tools\tool.exe /silent", @"C:\Tools\tool.exe")]
    [InlineData("", null)]
    public void ExtractExecutable_HandlesQuotedAndBareCommands(string command, string? expected)
    {
        Assert.Equal(expected, WindowsStartupManager.ExtractExecutable(command));
    }
}
