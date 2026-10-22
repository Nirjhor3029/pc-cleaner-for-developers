using PcCleaner.Application.Services;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Scanners;

namespace PcCleaner.UnitTests;

public class RecycleBinAndScanServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public RecycleBinAndScanServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-RecycleBin-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            foreach (var f in Directory.EnumerateFiles(_tempRoot, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(_tempRoot, true);
        }
    }

    [Fact]
    public void RecycleBin_FindsHiddenSystemEntries_AndSkipsDesktopIni()
    {
        var bin = Path.Combine(_tempRoot, "S-1-5-21-test");
        Directory.CreateDirectory(bin);
        File.SetAttributes(bin, FileAttributes.Hidden | FileAttributes.System | FileAttributes.Directory);

        File.WriteAllText(Path.Combine(bin, "desktop.ini"), "[.ShellClassInfo]");
        File.WriteAllBytes(Path.Combine(bin, "$IABC.txt"), new byte[10]);
        File.WriteAllBytes(Path.Combine(bin, "$RABC.txt"), new byte[100]);
        var recycledFolder = Path.Combine(bin, "$RDEF");
        Directory.CreateDirectory(Path.Combine(recycledFolder, "sub"));
        File.WriteAllBytes(Path.Combine(recycledFolder, "a.bin"), new byte[200]);
        File.WriteAllBytes(Path.Combine(recycledFolder, "sub", "b.bin"), new byte[300]);

        var items = RecycleBinScanner.ScanBinDirectory(bin);

        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, i => i.FilePath.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(500, items.Single(i => i.FilePath == recycledFolder).SizeBytes);
        Assert.Equal(610, items.Sum(i => i.SizeBytes));
    }

    [Fact]
    public async Task ScanService_FailingScanner_DoesNotAbortScan()
    {
        var service = new ScanService(new IScanner[] { new ThrowingScanner(), new FixedScanner() });

        var results = await service.RunAllScannersAsync();

        Assert.Equal(2, results.Count);
        Assert.Empty(results[0].Items);
        Assert.Equal(ScannerType.UserTemp, results[0].Type);
        Assert.Single(results[1].Items);
    }

    private sealed class ThrowingScanner : IScanner
    {
        public string Name => "Throwing";
        public ScannerType Type => ScannerType.UserTemp;
        public bool RequiresAdmin => false;
        public Task<ScanResult> ScanAsync() => throw new UnauthorizedAccessException();
    }

    private sealed class FixedScanner : IScanner
    {
        public string Name => "Fixed";
        public ScannerType Type => ScannerType.NpmCache;
        public bool RequiresAdmin => false;
        public Task<ScanResult> ScanAsync() => Task.FromResult(new ScanResult
        {
            ScannerName = Name,
            Type = Type,
            Items = { new ScanItem { FilePath = "x", SizeBytes = 1 } }
        });
    }
}
