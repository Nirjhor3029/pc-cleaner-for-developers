using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Models;

namespace PcCleaner.UnitTests;

public class DomainModelTests
{
    [Fact]
    public void ScanResult_TotalSizeBytes_SumOfItemSizes()
    {
        var result = new ScanResult
        {
            ScannerName = "Test",
            Type = ScannerType.UserTemp,
            Items =
            {
                new ScanItem { FilePath = "a.tmp", SizeBytes = 100 },
                new ScanItem { FilePath = "b.tmp", SizeBytes = 250 }
            }
        };

        Assert.Equal(350L, result.TotalSizeBytes);
    }

    [Fact]
    public void ScanResult_ItemCount_ReturnsNumberOfItems()
    {
        var result = new ScanResult
        {
            Items =
            {
                new ScanItem(),
                new ScanItem(),
                new ScanItem()
            }
        };

        Assert.Equal(3, result.ItemCount);
    }

    [Fact]
    public void ScanItem_DefaultsToSelectedAndDeletable()
    {
        var item = new ScanItem();

        Assert.True(item.IsSelected);
        Assert.True(item.IsDeletable);
    }

    [Fact]
    public void CleaningResult_DefaultsToEmpty()
    {
        var result = new CleaningResult();

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.FilesSkipped);
        Assert.Empty(result.SkippedReasons);
    }
}