using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Models;

namespace PcCleaner.Domain.Interfaces;

public interface IScanner
{
    string Name { get; }
    ScannerType Type { get; }
    bool RequiresAdmin { get; }
    Task<ScanResult> ScanAsync();
}