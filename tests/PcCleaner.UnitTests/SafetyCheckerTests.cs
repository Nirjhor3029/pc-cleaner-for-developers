using PcCleaner.Infrastructure.Safety;

namespace PcCleaner.UnitTests;

public class SafetyCheckerTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly SafetyChecker _checker;

    public SafetyCheckerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PcCleaner-Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _checker = new SafetyChecker();
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
    public void IsWithinAllowedDirectory_ReturnsTrue_ForChildPath()
    {
        var file = Path.Combine(_tempRoot, "file.tmp");

        Assert.True(_checker.IsWithinAllowedDirectory(file, _tempRoot));
    }

    [Fact]
    public void IsWithinAllowedDirectory_ReturnsFalse_ForOutsidePath()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "SomeOtherDir", "file.tmp");

        Assert.False(_checker.IsWithinAllowedDirectory(outsidePath, _tempRoot));
    }

    [Fact]
    public void IsSafeToDelete_ReturnsTrue_ForNormalFileInAllowedDir()
    {
        var file = Path.Combine(_tempRoot, "junk.tmp");
        File.WriteAllText(file, "content");

        Assert.True(_checker.IsSafeToDelete(file, _tempRoot));
    }

    [Fact]
    public void IsSafeToDelete_ReturnsFalse_ForFileOutsideAllowedDir()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "streetto.clean");
        // Create outside the allowed dir but inside temp
        var file = Path.Combine(_tempRoot, "..", "outside.tmp");
        string? full = null;
        try
        {
            full = Path.GetFullPath(file);
            File.WriteAllText(full, "x");
            Assert.False(_checker.IsSafeToDelete(full, _tempRoot));
        }
        finally
        {
            if (full != null && File.Exists(full))
                File.Delete(full);
        }
    }

    [Fact]
    public void IsSafeToDelete_ReturnsFalse_ForNonexistentFile()
    {
        var missing = Path.Combine(_tempRoot, "missing.tmp");

        Assert.False(_checker.IsSafeToDelete(missing, _tempRoot));
    }

    [Fact]
    public void IsFileLocked_ReturnsTrue_ForLockedFile()
    {
        var file = Path.Combine(_tempRoot, "locked.tmp");
        File.WriteAllText(file, "content");

        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.True(_checker.IsFileLocked(file));
        }

        Assert.False(_checker.IsFileLocked(file));
    }
}