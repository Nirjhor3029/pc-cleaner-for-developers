using PcCleaner.Infrastructure.Scanners.Apps;
using PcCleaner.Infrastructure.Scanners.Developer;
using PcCleaner.Infrastructure.Scanners.SystemCleanup;

namespace PcCleaner.UnitTests;

public class ExtraCleanupScannerTests : IDisposable
{
    private readonly string _tempRoot;

    public ExtraCleanupScannerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-Extra-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private string MakeFile(string relative, int size)
    {
        var path = Path.Combine(_tempRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    [Fact]
    public void Wps_KeepsNewestVersion_AndCleansOldVersionAndUpdateFiles()
    {
        MakeFile(@"install\12.1.0.28485\office6\wps.exe", 100);
        MakeFile(@"install\12.1.0.29162\office6\wps.exe", 100);
        MakeFile(@"install\ttttt\x", 5);
        MakeFile(@"data\office6\updatenew\WPS_25242.exe", 50);
        MakeFile(@"data\office6\cache\c.merge", 20);
        MakeFile(@"data\wps_intl\addons\keep.dll", 999);

        var result = new WpsOfficeScanner().Scan(
            Path.Combine(_tempRoot, "install"), Path.Combine(_tempRoot, "data"), Array.Empty<string>());

        Assert.Equal(3, result.ItemCount);
        Assert.Contains(result.Items, i => i.FilePath.EndsWith("12.1.0.28485") && i.SizeBytes == 100);
        Assert.DoesNotContain(result.Items, i => i.FilePath.Contains("29162") || i.FilePath.Contains("addons") || i.FilePath.Contains("ttttt"));
        Assert.Equal(170, result.TotalSizeBytes);
    }

    [Fact]
    public void Wps_SkipsOldVersion_WhenAProcessRunsFromIt()
    {
        var oldExe = MakeFile(@"install\12.1.0.28485\office6\wps.exe", 100);
        MakeFile(@"install\12.1.0.29162\office6\wps.exe", 100);

        var result = new WpsOfficeScanner().Scan(
            Path.Combine(_tempRoot, "install"), Path.Combine(_tempRoot, "data"), new[] { oldExe });

        Assert.Empty(result.Items);
    }

    [Fact]
    public void NpmLeftovers_FindsRenamedUpdateFolders_Only()
    {
        MakeFile(@"node_modules\@anthropic-ai\claude-code\cli.js", 10);
        MakeFile(@"node_modules\@anthropic-ai\.claude-code-GnGzw2as\cli.js", 30);
        MakeFile(@"node_modules\.opencode-ai-jb5WO5Lb\bin", 20);
        MakeFile(@"node_modules\opencode-ai\bin", 10);
        MakeFile(@"node_modules\.bin\tool", 1);

        var result = new NpmGlobalLeftoversScanner().ScanNodeModules(Path.Combine(_tempRoot, "node_modules"));

        Assert.Equal(2, result.ItemCount);
        Assert.Equal(50, result.TotalSizeBytes);
        Assert.All(result.Items, i => Assert.Matches(@"\\\.[a-z-]+-[A-Za-z0-9]{8}$", i.FilePath));
    }

    [Fact]
    public void Hibernation_ReportsHiberfilSize_AsOffByDefaultCommand()
    {
        var hiberfil = MakeFile("hiberfil.sys", 4096);

        var result = new HibernationScanner().ScanFile(hiberfil);

        var item = Assert.Single(result.Items);
        Assert.Equal(4096, item.SizeBytes);
        Assert.Equal("/hibernate off", item.Command!.Arguments);
        Assert.False(result.SelectedByDefault);
        Assert.Empty(new HibernationScanner().ScanFile(Path.Combine(_tempRoot, "missing.sys")).Items);
    }

    [Fact]
    public void DockerCompact_WritesDiskpartScript_AndBlocksWhileDockerRuns()
    {
        var vhdx = MakeFile(@"wsl\disk\docker_data.vhdx", 10);
        var scriptDir = Path.Combine(_tempRoot, "scripts");

        var stopped = new DockerDiskCompactScanner().Scan(Path.Combine(_tempRoot, "wsl"), scriptDir, dockerRunning: false);
        var running = new DockerDiskCompactScanner().Scan(Path.Combine(_tempRoot, "wsl"), scriptDir, dockerRunning: true);

        var item = Assert.Single(stopped.Items);
        Assert.True(item.IsDeletable);
        Assert.StartsWith("/c wsl.exe --shutdown && diskpart.exe /s", item.Command!.Arguments);
        var script = File.ReadAllLines(Path.Combine(scriptDir, "compact-docker-vhdx.txt"));
        Assert.Equal($"select vdisk file=\"{vhdx}\"", script[0]);
        Assert.Contains("compact vdisk", script);
        Assert.False(running.Items.Single().IsDeletable);
        Assert.True(stopped.SizeUnknown);
    }

    [Fact]
    public async Task CacheScanner_DoesNotFollowJunctions_OutOfTheCache()
    {
        MakeFile(@"cache\real.bin", 10);
        var outside = MakeFile(@"outside\precious.docx", 999);
        var junction = Path.Combine(_tempRoot, @"cache\link");
        // Junctions need no admin rights (bun's cache is full of them)
        var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
            $"/c mklink /J \"{junction}\" \"{Path.GetDirectoryName(outside)}\"") { CreateNoWindow = true, UseShellExecute = false })!;
        mklink.WaitForExit();
        Assert.True(Directory.Exists(Path.Combine(junction)), "junction was not created");

        var result = await new NpmCacheScanner().ScanPathAsync(Path.Combine(_tempRoot, "cache"));

        var item = Assert.Single(result.Items);
        Assert.EndsWith("real.bin", item.FilePath);
        Directory.Delete(junction); // removes only the link, before Dispose deletes the tree
    }

    [Fact]
    public void DockerCompact_NoVhdx_ReturnsEmpty()
    {
        var result = new DockerDiskCompactScanner().Scan(Path.Combine(_tempRoot, "none"), Path.Combine(_tempRoot, "s"), false);
        Assert.Empty(result.Items);
    }
}
