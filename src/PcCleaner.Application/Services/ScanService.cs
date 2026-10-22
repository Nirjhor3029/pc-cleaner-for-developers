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
            results.Add(await RunIsolatedAsync(scanners[i]));
        }
        return results;
    }

    /// <summary>
    /// Several scanners do their file enumeration synchronously before returning
    /// a completed Task, so run each on the thread pool to keep the UI responsive.
    /// A failing scanner yields an empty result instead of aborting the whole scan.
    /// </summary>
    private static async Task<ScanResult> RunIsolatedAsync(IScanner scanner)
    {
        ScanResult result;
        try
        {
            result = await Task.Run(scanner.ScanAsync);
        }
        catch
        {
            result = new ScanResult { ScannerName = scanner.Name, Type = scanner.Type };
        }
        result.RequiresAdmin = scanner.RequiresAdmin;
        return result;
    }

    public async Task<ScanResult> RunScannerAsync(ScannerType type)
    {
        var scanner = _scanners.First(s => s.Type == type);
        return await RunIsolatedAsync(scanner);
    }
}