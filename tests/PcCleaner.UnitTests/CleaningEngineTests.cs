using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Cleaning;

namespace PcCleaner.UnitTests;

public class CleaningEngineTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly CleaningEngine _engine;

    public CleaningEngineTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-Clean-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _engine = new CleaningEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, true);
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private ScanItem CreateItem(string name, long size, bool selected = true, bool deletable = true)
    {
        var path = Path.Combine(_tempRoot, name);
        File.WriteAllBytes(path, new byte[size]);
        return new ScanItem
        {
            FilePath = path,
            SizeBytes = size,
            IsSelected = selected,
            IsDeletable = deletable
        };
    }

    [Fact]
    public async Task CleanAsync_DeletesSelectedFiles()
    {
        var item = CreateItem("junk1.tmp", 100);
        var item2 = CreateItem("junk2.tmp", 200);

        var result = await _engine.CleanAsync([item, item2]);

        Assert.Equal(2, result.FilesDeleted);
        Assert.Equal(300, result.RemovedBytes);
        Assert.Equal(0, result.FilesSkipped);
        Assert.False(File.Exists(item.FilePath));
        Assert.False(File.Exists(item2.FilePath));
    }

    [Fact]
    public async Task CleanAsync_DoesNotDeleteUnselectedItems()
    {
        var item = CreateItem("keep.txt", 100, selected: false);
        var item2 = CreateItem("delete.tmp", 50);

        var result = await _engine.CleanAsync([item, item2]);

        Assert.Equal(1, result.FilesDeleted);
        Assert.True(File.Exists(item.FilePath));
        Assert.False(File.Exists(item2.FilePath));
    }

    [Fact]
    public async Task CleanAsync_SkipsNonDeletableItems()
    {
        var item = CreateItem("protected.tmp", 100, deletable: false);

        var result = await _engine.CleanAsync([item]);

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(1, result.FilesSkipped);
        Assert.Equal(100, result.SkippedBytes);
        Assert.Contains(result.SkippedReasons, r => r.Contains("non-deletable"));
        Assert.True(File.Exists(item.FilePath));
    }

    [Fact]
    public async Task CleanAsync_SkipsLockedFiles()
    {
        var path = Path.Combine(_tempRoot, "locked.tmp");
        File.WriteAllBytes(path, new byte[100]);

        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var item = new ScanItem
            {
                FilePath = path,
                SizeBytes = 100,
                IsSelected = true,
                IsDeletable = true
            };

            var result = await _engine.CleanAsync([item]);

            Assert.Equal(0, result.FilesDeleted);
            Assert.Equal(1, result.FilesSkipped);
            Assert.Contains(result.SkippedReasons, r => r.Contains("File in use"));
        }

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task CleanAsync_SkipsMissingFiles()
    {
        var item = new ScanItem
        {
            FilePath = Path.Combine(_tempRoot, "missing.tmp"),
            SizeBytes = 50,
            IsSelected = true,
            IsDeletable = true
        };

        var result = await _engine.CleanAsync([item]);

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(1, result.FilesSkipped);
        Assert.Contains(result.SkippedReasons, r => r.Contains("File not found"));
    }

    [Fact]
    public async Task CleanAsync_ReportsFoundBytes()
    {
        var item = CreateItem("junk.tmp", 100);
        var item2 = CreateItem("keep.txt", 50, selected: false);

        var result = await _engine.CleanAsync([item, item2]);

        Assert.Equal(150, result.FoundBytes);
    }

    [Fact]
    public async Task CleanAsync_ReportsProgress()
    {
        var items = new List<ScanItem>();
        for (int i = 0; i < 30; i++)
            items.Add(CreateItem($"junk{i}.tmp", 10));

        var reports = new List<CleanUpProgress>();
        var progress = new Progress<CleanUpProgress>(p => reports.Add(p));

        var result = await _engine.CleanAsync(items, progress);

        Assert.Equal(30, result.FilesDeleted);
        Assert.NotEmpty(reports);
        Assert.All(reports, r => Assert.Equal(30, r.TotalFiles));
        Assert.True(reports.Last().FilesProcessed >= 25, "Final report should be near 100%.");
        Assert.True(reports.Last().Percent >= 83.3, "Final progress should be >= 25/30.");
    }

    [Fact]
    public void CleanUpProgress_Percent_ComputesRatio()
    {
        var p = new CleanUpProgress { TotalFiles = 4, FilesProcessed = 2 };
        Assert.Equal(50, p.Percent);
        Assert.Equal(0, new CleanUpProgress { TotalFiles = 0, FilesProcessed = 0 }.Percent);
    }
}