using PcCleaner.Domain.Enums;
using PcCleaner.Infrastructure.Browser;
using PcCleaner.Infrastructure.Scanners.Apps;
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
        var gradle = new GradleCacheScanner();
        var androidStudio = new AndroidStudioCacheScanner();
        var nuget = new NuGetCacheScanner();
        var huggingFace = new HuggingFaceCacheScanner();
        var teams = new TeamsCacheScanner();
        var slack = new SlackCacheScanner();
        var discord = new DiscordCacheScanner();
        var pnpm = new PnpmCacheScanner();
        var bun = new BunCacheScanner();
        var maven = new MavenCacheScanner();
        var pip = new PipCacheScanner();
        var flutter = new FlutterPubCacheScanner();

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

        Assert.Equal("Gradle Cache", gradle.Name);
        Assert.Equal(ScannerType.GradleCache, gradle.Type);
        Assert.False(gradle.RequiresAdmin);

        Assert.Equal("Android Studio Caches", androidStudio.Name);
        Assert.Equal(ScannerType.AndroidStudioCache, androidStudio.Type);
        Assert.False(androidStudio.RequiresAdmin);

        Assert.Equal("NuGet HTTP Cache", nuget.Name);
        Assert.Equal(ScannerType.NuGetCache, nuget.Type);

        Assert.Equal("AI Model Cache (Hugging Face)", huggingFace.Name);
        Assert.Equal(ScannerType.HuggingFaceCache, huggingFace.Type);
        Assert.False(huggingFace.RequiresAdmin);

        Assert.Equal("Microsoft Teams Cache", teams.Name);
        Assert.Equal(ScannerType.TeamsCache, teams.Type);
        Assert.False(teams.RequiresAdmin);

        Assert.Equal("Slack Cache", slack.Name);
        Assert.Equal(ScannerType.SlackCache, slack.Type);
        Assert.False(slack.RequiresAdmin);

        Assert.Equal("Discord Cache", discord.Name);
        Assert.Equal(ScannerType.DiscordCache, discord.Type);
        Assert.False(discord.RequiresAdmin);

        Assert.Equal("pnpm Cache", pnpm.Name);
        Assert.Equal(ScannerType.PnpmCache, pnpm.Type);

        Assert.Equal("Bun Cache", bun.Name);
        Assert.Equal(ScannerType.BunCache, bun.Type);

        Assert.Equal("Maven Wrapper Cache", maven.Name);
        Assert.Equal(ScannerType.MavenCache, maven.Type);

        Assert.Equal("pip Cache", pip.Name);
        Assert.Equal(ScannerType.PipCache, pip.Type);

        Assert.Equal("Flutter Pub Cache", flutter.Name);
        Assert.Equal(ScannerType.FlutterPubCache, flutter.Type);
        Assert.False(flutter.RequiresAdmin);
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
        Assert.Equal(0, (await new NuGetCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new GradleCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new HuggingFaceCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new AndroidStudioCacheScanner().ScanDirectoryAsync(missing)).ItemCount);
        Assert.Equal(0, (await new TeamsCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new SlackCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new DiscordCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new PnpmCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new BunCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new MavenCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new PipCacheScanner().ScanPathAsync(missing)).ItemCount);
        Assert.Equal(0, (await new FlutterPubCacheScanner().ScanPathAsync(missing)).ItemCount);
    }

    [Fact]
    public async Task AppCacheScanner_ScansCacheSubdirsOnly_IgnoresProfileData()
    {
        var product = Path.Combine(_tempRoot, "Discord");
        Directory.CreateDirectory(Path.Combine(product, "Cache"));
        Directory.CreateDirectory(Path.Combine(product, "GPUCache"));
        Directory.CreateDirectory(Path.Combine(product, "Local Storage", "leveldb"));
        Directory.CreateDirectory(Path.Combine(product, "IndexedDB", "leveldb"));
        File.WriteAllBytes(Path.Combine(product, "Cache", "f_data_0"), new byte[400]);
        File.WriteAllBytes(Path.Combine(product, "GPUCache", "gp.bin"), new byte[150]);
        File.WriteAllBytes(Path.Combine(product, "Local Storage", "leveldb", "000003.log"), new byte[9999]);
        File.WriteAllBytes(Path.Combine(product, "IndexedDB", "leveldb", "idx.db"), new byte[9999]);

        var discord = new DiscordCacheScanner();
        var result = await discord.ScanPathAsync(product);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(550, result.TotalSizeBytes);
        Assert.All(result.Items, i => Assert.True(i.IsDeletable));
    }

    [Fact]
    public async Task TeamsCacheScanner_ScansBlobStorageAndLogs()
    {
        var product = Path.Combine(_tempRoot, "Teams");
        Directory.CreateDirectory(Path.Combine(product, "blob_storage"));
        Directory.CreateDirectory(Path.Combine(product, "logs"));
        File.WriteAllBytes(Path.Combine(product, "blob_storage", "blob.bin"), new byte[600]);
        File.WriteAllBytes(Path.Combine(product, "logs", "teams.log"), new byte[200]);

        var teams = new TeamsCacheScanner();
        var result = await teams.ScanPathAsync(product);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(800, result.TotalSizeBytes);
    }

    [Fact]
    public async Task GradleCacheScanner_ScansMultipleRoots()
    {
        var caches = Path.Combine(_tempRoot, "caches");
        var dists = Path.Combine(_tempRoot, "wrapper", "dists");
        Directory.CreateDirectory(Path.Combine(caches, "modules-2"));
        Directory.CreateDirectory(dists);
        File.WriteAllBytes(Path.Combine(caches, "modules-2", "a.jar"), new byte[120]);
        File.WriteAllBytes(Path.Combine(dists, "gradle-8.9-bin.zip"), new byte[880]);

        var gradle = new GradleCacheScanner();
        var result = await gradle.ScanPathAsync(_tempRoot);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(1000, result.TotalSizeBytes);
        Assert.All(result.Items, i => Assert.True(i.IsDeletable));
    }

    [Fact]
    public async Task AndroidStudioCacheScanner_ScansCachesAndLogsOnly()
    {
        var product = Path.Combine(_tempRoot, "AndroidStudio2024.1");
        var caches = Path.Combine(product, "caches");
        var logs = Path.Combine(product, "log");
        var settings = Path.Combine(product, "options", "editor.xml");
        Directory.CreateDirectory(caches);
        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
        File.WriteAllBytes(Path.Combine(caches, "cache.bin"), new byte[300]);
        File.WriteAllBytes(Path.Combine(logs, "idea.log"), new byte[100]);
        File.WriteAllBytes(settings, new byte[9999]);

        var androidStudio = new AndroidStudioCacheScanner();
        var result = await androidStudio.ScanDirectoryAsync(product);

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(400, result.TotalSizeBytes);
        Assert.DoesNotContain(result.Items, i => i.FilePath == settings);
    }

    [Fact]
    public async Task NuGetCacheScanner_ScansHttpCacheOnly()
    {
        var cacheDir = Path.Combine(_tempRoot, "v3-cache");
        Directory.CreateDirectory(cacheDir);
        File.WriteAllBytes(Path.Combine(cacheDir, "pkg.resource"), new byte[250]);

        var scanner = new NuGetCacheScanner();
        var result = await scanner.ScanPathAsync(_tempRoot);

        Assert.Equal(1, result.ItemCount);
        Assert.Equal(250, result.TotalSizeBytes);
    }

    [Fact]
    public async Task HuggingFaceCacheScanner_ScansModelCache()
    {
        var hub = Path.Combine(_tempRoot, "hub");
        Directory.CreateDirectory(Path.Combine(hub, "models--gpt2"));
        File.WriteAllBytes(Path.Combine(hub, "models--gpt2", "model.bin"), new byte[640]);

        var scanner = new HuggingFaceCacheScanner();
        var result = await scanner.ScanPathAsync(_tempRoot);

        Assert.Equal(1, result.ItemCount);
        Assert.Equal(640, result.TotalSizeBytes);
        Assert.All(result.Items, i => Assert.True(i.IsDeletable));
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
        Assert.Equal(CleanupSection.Developer, ScannerType.GradleCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.AndroidStudioCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.NuGetCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.HuggingFaceCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.PnpmCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.BunCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.MavenCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.PipCache.Section());
        Assert.Equal(CleanupSection.Developer, ScannerType.FlutterPubCache.Section());
        Assert.Equal(CleanupSection.Apps, ScannerType.TeamsCache.Section());
        Assert.Equal(CleanupSection.Apps, ScannerType.SlackCache.Section());
        Assert.Equal(CleanupSection.Apps, ScannerType.DiscordCache.Section());
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