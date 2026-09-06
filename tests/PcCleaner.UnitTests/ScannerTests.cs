using PcCleaner.Domain.Enums;
using PcCleaner.Infrastructure.Browser;
using PcCleaner.Infrastructure.Scanners;

namespace PcCleaner.UnitTests;

public class ScannerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly UserTempScanner _userTempScanner;
    private readonly WindowsTempScanner _windowsTempScanner;

    public ScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-Scanner-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _userTempScanner = new UserTempScanner();
        _windowsTempScanner = new WindowsTempScanner();
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
    public void Scanners_HaveCorrectMetadata()
    {
        Assert.Equal("User Temp", _userTempScanner.Name);
        Assert.Equal(ScannerType.UserTemp, _userTempScanner.Type);
        Assert.False(_userTempScanner.RequiresAdmin);

        Assert.Equal("Windows Temp", _windowsTempScanner.Name);
        Assert.Equal(ScannerType.WindowsTemp, _windowsTempScanner.Type);
        Assert.True(_windowsTempScanner.RequiresAdmin);
    }

    [Fact]
    public async Task UserTempScanner_FindsFilesAndSumsSize()
    {
        var subDir = Path.Combine(_tempRoot, "sub");
        Directory.CreateDirectory(subDir);
        File.WriteAllBytes(Path.Combine(_tempRoot, "a.tmp"), new byte[100]);
        File.WriteAllBytes(Path.Combine(subDir, "b.tmp"), new byte[250]);
        File.WriteAllBytes(Path.Combine(_tempRoot, "c.txt"), new byte[50]);

        var result = await _userTempScanner.ScanDirectoryAsync(_tempRoot);

        Assert.Equal(3, result.ItemCount);
        Assert.Equal(400, result.TotalSizeBytes);
        Assert.Equal("User Temp", result.ScannerName);
    }

    [Fact]
    public async Task UserTempScanner_ReturnsEmpty_ForMissingDirectory()
    {
        var missingDir = Path.Combine(_tempRoot, "does-not-exist");

        var result = await _userTempScanner.ScanDirectoryAsync(missingDir);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalSizeBytes);
    }

    [Fact]
    public async Task UserTempScanner_SkipsInaccessibleDirectories()
    {
        // Just ensure it doesn't throw when enumerating a complex tree
        for (int i = 0; i < 5; i++)
        {
            var dir = Path.Combine(_tempRoot, $"folder{i}");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "f.tmp"), new byte[10]);
        }

        var result = await _userTempScanner.ScanDirectoryAsync(_tempRoot);

        Assert.Equal(5, result.ItemCount);
        Assert.Equal(50, result.TotalSizeBytes);
    }

    [Fact]
    public void ChromeCacheScanner_HasCorrectMetadata()
    {
        var scanner = new ChromeCacheScanner();

        Assert.Equal("Chrome Cache", scanner.Name);
        Assert.Equal(ScannerType.ChromeCache, scanner.Type);
        Assert.False(scanner.RequiresAdmin);
    }

    [Fact]
    public void EdgeCacheScanner_HasCorrectMetadata()
    {
        var scanner = new EdgeCacheScanner();

        Assert.Equal("Edge Cache", scanner.Name);
        Assert.Equal(ScannerType.EdgeCache, scanner.Type);
        Assert.False(scanner.RequiresAdmin);
    }

    [Fact]
    public void RecycleBinScanner_HasCorrectMetadata()
    {
        var scanner = new RecycleBinScanner();

        Assert.Equal("Recycle Bin", scanner.Name);
        Assert.Equal(ScannerType.RecycleBin, scanner.Type);
        Assert.False(scanner.RequiresAdmin);
    }

    [Fact]
    public async Task ChromeCacheScanner_DoesNotThrow_WhenCacheMissing()
    {
        // Cache path is user-specific; if missing it should return empty, not throw
        var scanner = new ChromeCacheScanner();

        bool released = false;
        try
        {
            var result = await scanner.ScanAsync();
            released = true;
        }
        catch
        {
            // test fails below
        }

        Assert.True(released, "ScanAsync should not throw even if cache is missing");
    }
}