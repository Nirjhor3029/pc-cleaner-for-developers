using PcCleaner.Domain.Enums;
using PcCleaner.Infrastructure.Browser;
using PcCleaner.Infrastructure.Scanners.Developer;

namespace PcCleaner.UnitTests;

public class DeveloperScannerTests : IDisposable
{
    private readonly string _tempRoot;

    public DeveloperScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-DevScanner-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
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
    public void DeveloperScanners_HaveCorrectMetadata()
    {
        var npm = new NpmCacheScanner();
        var yarn = new YarnCacheScanner();
        var composer = new ComposerCacheScanner();
        var vscode = new VSCodeCacheScanner();
        var playwright = new PlaywrightCacheScanner();

        Assert.Equal("npm Cache", npm.Name);
        Assert.Equal(ScannerType.NpmCache, npm.Type);
        Assert.False(npm.RequiresAdmin);

        Assert.Equal("Yarn Cache", yarn.Name);
        Assert.Equal(ScannerType.YarnCache, yarn.Type);

        Assert.Equal("Composer Cache", composer.Name);
        Assert.Equal(ScannerType.ComposerCache, composer.Type);

        Assert.Equal("VS Code Cache", vscode.Name);
        Assert.Equal(ScannerType.VSCodeCache, vscode.Type);

        Assert.Equal("Playwright Browsers", playwright.Name);
        Assert.Equal(ScannerType.PlaywrightCache, playwright.Type);
    }

    [Fact]
    public async Task NpmCacheScanner_FindsFiles_WhenCachePathProvided()
    {
        File.WriteAllBytes(Path.Combine(_tempRoot, "file-1"), new byte[120]);
        Directory.CreateDirectory(Path.Combine(_tempRoot, "_cacache"));
        File.WriteAllBytes(Path.Combine(_tempRoot, "_cacache", "nested.bin"), new byte[380]);

        var scanner = new NpmCacheScanner();
        var result = await scanner.ScanPathAsync(_tempRoot);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(500, result.TotalSizeBytes);
        Assert.All(result.Items, i => Assert.True(i.IsDeletable));
    }

    [Fact]
    public async Task CacheScanner_ReturnsEmpty_ForMissingPath()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist");

        Assert.Equal(0, (await new NpmCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new YarnCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new ComposerCacheScanner().ScanPathAsync(missing)).ItemCount);
    }

    [Fact]
    public async Task VSCodeCacheScanner_ScansKnownCacheSubdirsOnly()
    {
        var cache = Path.Combine(_tempRoot, "Cache");
        var codeCache = Path.Combine(_tempRoot, "Code Cache");
        var logs = Path.Combine(_tempRoot, "logs");
        var settings = Path.Combine(_tempRoot, "settings.json");

        Directory.CreateDirectory(cache);
        Directory.CreateDirectory(codeCache);
        Directory.CreateDirectory(logs);
        File.WriteAllBytes(Path.Combine(cache, "a.bin"), new byte[100]);
        File.WriteAllBytes(Path.Combine(codeCache, "b.bin"), new byte[200]);
        File.WriteAllBytes(Path.Combine(logs, "c.log"), new byte[50]);
        File.WriteAllBytes(settings, new byte[9999]);

        var scanner = new VSCodeCacheScanner();
        var result = await scanner.ScanDirectoryAsync(_tempRoot);

        Assert.Equal(3, result.ItemCount);
        Assert.Equal(350, result.TotalSizeBytes);
        Assert.DoesNotContain(result.Items, i => i.FilePath == settings);
    }

    [Fact]
    public async Task PlaywrightCacheScanner_ScansAllBrowserDirs()
    {
        var chrome = Path.Combine(_tempRoot, "chromium-1208");
        var ff = Path.Combine(_tempRoot, "ffmpeg-1011");
        Directory.CreateDirectory(chrome);
        Directory.CreateDirectory(ff);
        File.WriteAllBytes(Path.Combine(chrome, "chrome.exe"), new byte[800]);
        File.WriteAllBytes(Path.Combine(ff, "ffmpeg.exe"), new byte[200]);

        var scanner = new PlaywrightCacheScanner();
        var result = await scanner.ScanDirectoryAsync(_tempRoot);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(1000, result.TotalSizeBytes);
    }

    [Fact]
    public void DeveloperScannerTypes_MapToDeveloperSection()
    {
        Assert.Equal(CleanupSection.Developer, ScannerType.NpmCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.YarnCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.ComposerCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.VSCodeCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.PlaywrightCache.Section());
        Assert.Equal(CleanupSection.System, ScannerType.UserTemp.Section());
        Assert.Equal(CleanupSection.System, ScannerType.ChromeCache.Section());
    }
}

public class BrowserProfileDiscoveryTests : IDisposable
{
    private readonly string _tempRoot;

    public BrowserProfileDiscoveryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-BrowserProfile-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
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
    public async Task BrowserScanner_DiscoversAllProfiles()
    {
        foreach (var profile in new[] { "Default", "Profile 1", "Profile 16", "Profile 2" })
        {
            var cacheDir = Path.Combine(_tempRoot, profile, "Cache");
            var codeCache = Path.Combine(_tempRoot, profile, "Code Cache");
            Directory.CreateDirectory(cacheDir);
            Directory.CreateDirectory(codeCache);
            File.WriteAllBytes(Path.Combine(cacheDir, "f1"), new byte[10]);
            File.WriteAllBytes(Path.Combine(codeCache, "f2"), new byte[10]);
        }
        // A non-profile dir must be ignored
        Directory.CreateDirectory(Path.Combine(_tempRoot, "GrShaderCache"));

        var chrome = new ChromeCacheScanner();
        var result = await chrome.ScanProfilesAsync(_tempRoot);

        Assert.Equal(8, result.ItemCount);
        Assert.All(result.Items, i => Assert.True(i.FilePath.Contains(_tempRoot)));
    }

    [Fact]
    public async Task BrowserScanner_MarksItemsNonDeletable_WhenBrowserRunning()
    {
        var cacheDir = Path.Combine(_tempRoot, "Default", "Cache");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, "f"), new byte[10]);

        var chrome = new ChromeCacheScanner();
        var result = await chrome.ScanProfilesAsync(_tempRoot, browserRunning: true);

        Assert.Single(result.Items);
        Assert.False(result.Items[0].IsDeletable);
    }

    [Fact]
    public async Task BrowserScanner_ReturnsEmpty_ForMissingRoot()
    {
        var chrome = new ChromeCacheScanner();
        var result = await chrome.ScanProfilesAsync(Path.Combine(_tempRoot, "nope"));
        Assert.Empty(result.Items);
    }
}