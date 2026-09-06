using PcCleaner.Application.Interfaces;
using PcCleaner.Application.Models;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.Application.Services;

public class ScanService : IScanService
{
    private readonly IEnumerable<IScanner> _scanners;

    public ScanService(IEnumerable<IScanner> scanners)
    {
        _scanners = scanners;
    }

    public async Task<List<ScanResult>> RunAllScannersAsync()
    {
        return await RunAllScannersAsync(null);
    }

    public async Task<List<ScanResult>> RunAllScannersAsync(IProgress<ScanProgress>? progress)
    {
        var scanners = _scanners.ToList();
        var results = new List<ScanResult>();
        for (int i = 0; i < scanners.Count; i++)
        {
            progress?.Report(new ScanProgress
            {
                CurrentIndex = i + 1,
                Total = scanners.Count,
                CategoryName = scanners[i].Name
            });
            var result = await scanners[i].ScanAsync();
            results.Add(result);
        }
        return results;
    }

    public async Task<ScanResult> RunScannerAsync(ScannerType type)
    {
        var scanner = _scanners.First(s => s.Type == type);
        return await scanner.ScanAsync();
    }
}