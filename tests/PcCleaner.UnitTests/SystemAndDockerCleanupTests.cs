using PcCleaner.Application.Services;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Cleaning;
using PcCleaner.Infrastructure.Commands;
using PcCleaner.Infrastructure.Scanners;
using PcCleaner.Infrastructure.Scanners.Developer;
using PcCleaner.Infrastructure.Scanners.SystemCleanup;

namespace PcCleaner.UnitTests;

public class SystemAndDockerCleanupTests : IDisposable
{
    private readonly string _tempRoot;

    public SystemAndDockerCleanupTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-SystemCleanup-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private const string DockerDfOutput =
        "{\"Active\":\"1\",\"Reclaimable\":\"2.1GB (60%)\",\"Size\":\"3.5GB\",\"TotalCount\":\"4\",\"Type\":\"Images\"}\n" +
        "{\"Active\":\"0\",\"Reclaimable\":\"0B\",\"Size\":\"0B\",\"TotalCount\":\"0\",\"Type\":\"Containers\"}\n" +
        "{\"Active\":\"1\",\"Reclaimable\":\"512.5MB (40%)\",\"Size\":\"1.2GB\",\"TotalCount\":\"3\",\"Type\":\"Local Volumes\"}\n" +
        "{\"Active\":\"0\",\"Reclaimable\":\"300kB\",\"Size\":\"300kB\",\"TotalCount\":\"7\",\"Type\":\"Build Cache\"}\n";

    [Theory]
    [InlineData("2.1GB (60%)", 2_100_000_000)]
    [InlineData("512.5MB", 512_500_000)]
    [InlineData("300kB", 300_000)]
    [InlineData("0B", 0)]
    [InlineData("garbage", 0)]
    [InlineData(null, 0)]
    public void DockerParseSize_UsesDecimalUnits(string? text, long expected)
    {
        Assert.Equal(expected, DockerDiskUsage.ParseSize(text));
    }

    [Fact]
    public async Task DockerCleanupScanner_AddsOnlyReclaimableCategories()
    {
        var runner = new FakeRunner(new CommandResult(0, DockerDfOutput, ""));
        var result = await new DockerCleanupScanner(runner).ScanAsync();

        Assert.Equal(2, result.Items.Count); // images + build cache, containers are 0B
        Assert.Contains(result.Items, i => i.Command == new CleanupCommand("docker", "image prune -a -f") && i.SizeBytes == 2_100_000_000);
        Assert.Contains(result.Items, i => i.Command == new CleanupCommand("docker", "builder prune -a -f"));
        Assert.True(result.SelectedByDefault);
    }

    [Fact]
    public async Task DockerVolumesScanner_IsOffByDefault_WithWarning()
    {
        var runner = new FakeRunner(new CommandResult(0, DockerDfOutput, ""));
        var result = await new DockerVolumesScanner(runner).ScanAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal(512_500_000, item.SizeBytes);
        Assert.False(result.SelectedByDefault);
        Assert.False(string.IsNullOrEmpty(result.Warning));
    }

    [Fact]
    public async Task DockerScanner_DaemonStoppedOrMissing_ReturnsEmpty()
    {
        var stopped = new FakeRunner(new CommandResult(1, "", "failed to connect to the docker API"));
        var missing = new FakeRunner(new System.ComponentModel.Win32Exception("not found"));

        Assert.Empty((await new DockerCleanupScanner(stopped).ScanAsync()).Items);
        Assert.Empty((await new DockerVolumesScanner(missing).ScanAsync()).Items);
    }

    [Fact]
    public async Task CleaningEngine_RunsCommandItems_AndCountsBytesOnSuccess()
    {
        var runner = new FakeRunner(new CommandResult(0, "", ""));
        var engine = new CleaningEngine(runner);
        var items = new List<ScanItem>
        {
            new() { FilePath = "Docker build cache", SizeBytes = 1000, Command = new CleanupCommand("docker", "builder prune -a -f") }
        };

        var result = await engine.CleanAsync(items);

        Assert.Equal(("docker", "builder prune -a -f"), runner.Calls.Single());
        Assert.Equal(1000, result.RemovedBytes);
        Assert.Equal(1, result.FilesDeleted);
    }

    [Fact]
    public async Task CleaningEngine_FailedCommand_IsSkippedWithReason()
    {
        var engine = new CleaningEngine(new FakeRunner(new CommandResult(5, "", "boom")));
        var items = new List<ScanItem>
        {
            new() { FilePath = "DISM component cleanup", SizeBytes = 0, Command = new CleanupCommand("dism.exe", "/x") }
        };

        var result = await engine.CleanAsync(items);

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(1, result.FilesSkipped);
        Assert.Contains("exit code 5", result.SkippedReasons.Single());
    }

    [Fact]
    public void DiskCleanupCommand_UsesHandlerSpecificSageSet()
    {
        var handler = new DiskCleanupHandler("Windows.old", "Previous Installations", 4201, Array.Empty<string>());

        var command = DiskCleanupScannerBase.BuildCommand(handler);

        Assert.Equal("cmd.exe", command.FileName);
        Assert.Contains(@"VolumeCaches\Previous Installations"" /v StateFlags4201", command.Arguments);
        Assert.EndsWith("cleanmgr.exe /sagerun:4201", command.Arguments);
    }

    [Fact]
    public async Task FileListScanner_ScansAllExistingRoots_IgnoresMissing()
    {
        var a = Directory.CreateDirectory(Path.Combine(_tempRoot, "D3DSCache")).FullName;
        var b = Directory.CreateDirectory(Path.Combine(_tempRoot, "NVIDIA", "DXCache", "sub")).FullName;
        File.WriteAllBytes(Path.Combine(a, "x.bin"), new byte[10]);
        File.WriteAllBytes(Path.Combine(b, "y.bin"), new byte[20]);

        var result = await new ShaderCacheScanner().ScanRootsAsync(new[] { a, b, Path.Combine(_tempRoot, "missing") });

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(30, result.TotalSizeBytes);
    }

    [Fact]
    public async Task ScanService_CopiesRequiresAdminToResult()
    {
        var service = new ScanService(new IScanner[] { new WindowsErrorReportingScanner(), new ShaderCacheScanner() });

        var results = await service.RunAllScannersAsync();

        Assert.True(results.Single(r => r.Type == ScannerType.WindowsErrorReporting).RequiresAdmin);
        Assert.False(results.Single(r => r.Type == ScannerType.ShaderCache).RequiresAdmin);
    }

    [Fact]
    public void DockerTypes_AreInDeveloperSection()
    {
        Assert.Equal(CleanupSection.Developer, ScannerType.DockerCleanup.Section());
        Assert.Equal(CleanupSection.System, ScannerType.WindowsOld.Section());
    }

    private sealed class FakeRunner : ICommandRunner
    {
        private readonly CommandResult? _result;
        private readonly Exception? _error;

        public FakeRunner(CommandResult result) => _result = result;
        public FakeRunner(Exception error) => _error = error;

        public List<(string, string)> Calls { get; } = new();

        public CommandResult Run(string fileName, string arguments, TimeSpan? timeout = null)
        {
            Calls.Add((fileName, arguments));
            if (_error != null)
                throw _error;
            return _result!;
        }
    }
}
