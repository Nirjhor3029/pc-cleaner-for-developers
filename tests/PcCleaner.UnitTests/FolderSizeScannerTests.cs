using PcCleaner.Infrastructure.DiskAnalyzer;

namespace PcCleaner.UnitTests;

public class FolderSizeScannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly FolderSizeScanner _scanner;

    public FolderSizeScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-Disk-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _scanner = new FolderSizeScanner();
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

    [Fact]
    public async Task GetEntriesAsync_ReturnsFilesAndFoldersWithSizes()
    {
        File.WriteAllBytes(Path.Combine(_tempRoot, "root.bin"), new byte[500]);

        var sub = Path.Combine(_tempRoot, "bigfolder");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "a.bin"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(sub, "b.bin"), new byte[2000]);

        var nested = Path.Combine(sub, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(nested, "c.bin"), new byte[700]);

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        var big = entries.Single(e => e.Name == "bigfolder");
        Assert.True(big.IsDirectory);
        Assert.Equal(3700, big.SizeBytes);
        Assert.Equal(3, big.FileCount);

        var rootFile = entries.Single(e => e.Name == "root.bin");
        Assert.False(rootFile.IsDirectory);
        Assert.Equal(500, rootFile.SizeBytes);
    }

    [Fact]
    public async Task GetEntriesAsync_SortedBySizeDescending()
    {
        var small = Path.Combine(_tempRoot, "small");
        Directory.CreateDirectory(small);
        File.WriteAllBytes(Path.Combine(small, "s.bin"), new byte[100]);

        var big = Path.Combine(_tempRoot, "big");
        Directory.CreateDirectory(big);
        File.WriteAllBytes(Path.Combine(big, "b.bin"), new byte[9000]);

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        Assert.Equal("big", entries[0].Name);
        Assert.True(entries[0].SizeBytes > entries[1].SizeBytes);
    }

    [Fact]
    public async Task GetEntriesAsync_ReturnsEmpty_ForMissingPath()
    {
        var entries = await _scanner.GetEntriesAsync(Path.Combine(_tempRoot, "missing"));

        Assert.Empty(entries);
    }

    [Fact]
    public async Task GetEntriesAsync_CountsNestedFilesRecursively()
    {
        var path = _tempRoot;
        for (int depth = 0; depth < 3; depth++)
        {
            var dir = Path.Combine(path, $"level{depth}");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "f.bin"), new byte[5]);
            path = dir;
        }

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        var first = entries[0];
        Assert.Equal(15, first.SizeBytes);
        Assert.Equal(3, first.FileCount);
    }

    [Fact]
    public async Task GetEntriesAsync_CountsHiddenFiles()
    {
        var hidden = Path.Combine(_tempRoot, "hidden.bin");
        File.WriteAllBytes(hidden, new byte[1234]);
        File.SetAttributes(hidden, FileAttributes.Hidden);

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        var entry = entries.Single(e => e.Name == "hidden.bin");
        Assert.Equal(1234, entry.SizeBytes);
        Assert.False(entry.IsProtected);
    }

    [Fact]
    public async Task GetEntriesAsync_FolderTotalIncludesHiddenFiles()
    {
        var sub = Path.Combine(_tempRoot, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllBytes(Path.Combine(sub, "normal.bin"), new byte[50]);
        var h = Path.Combine(sub, "hidden.bin");
        File.WriteAllBytes(h, new byte[150]);
        File.SetAttributes(h, FileAttributes.Hidden);

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        var dir = entries.Single(e => e.IsDirectory);
        Assert.Equal(200, dir.SizeBytes);
        Assert.Equal(2, dir.FileCount);
    }

    [Fact]
    public async Task GetEntriesAsync_SystemFilesAreCountedAndFlaggedProtected()
    {
        var sys = Path.Combine(_tempRoot, "sys.bin");
        File.WriteAllBytes(sys, new byte[777]);

        bool systemSet = true;
        try
        {
            File.SetAttributes(sys, FileAttributes.Hidden | FileAttributes.System);
        }
        catch (UnauthorizedAccessException)
        {
            Assert.True(false, "Test host could not set System attribute; run as admin to cover this case.");
        }
        catch (IOException)
        {
            Assert.True(false, "Test host could not set System attribute; run as admin to cover this case.");
        }

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        var entry = Assert.Single(entries);
        Assert.Equal("sys.bin", entry.Name);
        Assert.Equal(777, entry.SizeBytes);
        Assert.True(entry.IsProtected);
    }

    [Fact]
    public async Task GetEntriesAsync_SystemDirectoryIsFlaggedProtected()
    {
        var sysDir = Path.Combine(_tempRoot, "sysdir");
        Directory.CreateDirectory(sysDir);
        File.WriteAllBytes(Path.Combine(sysDir, "a.bin"), new byte[10]);

        try
        {
            File.SetAttributes(sysDir, FileAttributes.System);
        }
        catch (Exception)
        {
            return;
        }

        var entries = await _scanner.GetEntriesAsync(_tempRoot);

        var dir = Assert.Single(entries);
        Assert.True(dir.IsDirectory);
        Assert.True(dir.IsProtected);
        Assert.Equal(10, dir.SizeBytes);
    }
}