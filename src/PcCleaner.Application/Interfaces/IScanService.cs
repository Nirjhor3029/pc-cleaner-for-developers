using PcCleaner.Application.Models;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Models;

namespace PcCleaner.Application.Interfaces;

public interface IScanService
{
    Task<List<ScanResult>> RunAllScannersAsync();
    Task<List<ScanResult>> RunAllScannersAsync(IProgress<ScanProgress>? progress);
    Task<ScanResult> RunScannerAsync(ScannerType type);
}