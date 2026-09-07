using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using Serilog;

namespace PcCleaner.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISystemScanner _scanner;
    private readonly IHealthScoreService _health;
    private readonly IResourceMonitor _monitor;
    private readonly IStartupManager _startup;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private int overallScore = 0;
    [ObservableProperty] private string overallStatus = "Scanning...";
    [ObservableProperty] private double cpuPercent;
    [ObservableProperty] private double ramPercent;
    [ObservableProperty] private double diskPercent;
    [ObservableProperty] private double storageFreePercent;
    [ObservableProperty] private int startupCount;
    [ObservableProperty] private string cpuStatus = "—";
    [ObservableProperty] private string ramStatus = "—";
    [ObservableProperty] private string storageStatus = "—";
    [ObservableProperty] private List<RecommendedAction> actions = new();
    [ObservableProperty] private SystemSnapshot? snapshot;
    [ObservableProperty] private HealthScore? healthScore;
    [ObservableProperty] private bool isLoading = true;
    [ObservableProperty] private string lastScanText = "Never";

    public DashboardViewModel(ISystemScanner scanner, IHealthScoreService health, IResourceMonitor monitor, IStartupManager startup)
    {
        _scanner = scanner;
        _health = health;
        _monitor = monitor;
        _startup = startup;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var snap = await _scanner.ScanAsync(_cts.Token);
            // Enrich startup counts
            try
            {
                var entries = await _startup.GetAsync();
                snap = new SystemSnapshot
                {
                    Timestamp = snap.Timestamp,
                    CpuPercent = snap.CpuPercent,
                    CpuModel = snap.CpuModel,
                    CpuCores = snap.CpuCores,
                    CpuThreads = snap.CpuThreads,
                    RamTotalBytes = snap.RamTotalBytes,
                    RamUsedBytes = snap.RamUsedBytes,
                    RamAvailableBytes = snap.RamAvailableBytes,
                    DriveLetter = snap.DriveLetter,
                    StorageTotalBytes = snap.StorageTotalBytes,
                    StorageUsedBytes = snap.StorageUsedBytes,
                    StorageFreeBytes = snap.StorageFreeBytes,
                    DiskType = snap.DiskType,
                    DiskActivePercent = snap.DiskActivePercent,
                    StartupCount = entries.Count,
                    StartupHighImpactCount = entries.Count(x => x.Impact == Domain.Enums.StartupImpact.High),
                    TopCpu = (await _monitor.GetTopCpuAsync(3)).ToList(),
                    TopMemory = (await _monitor.GetTopMemoryAsync(3)).ToList()
                };
            }
            catch { }

            Snapshot = snap;
            var hs = _health.Calculate(snap);
            HealthScore = hs;
            OverallScore = hs.Overall;
            OverallStatus = hs.Overall >= 80 ? "Excellent" : hs.Overall >= 60 ? "Good" : hs.Overall >= 40 ? "Needs attention" : "Critical";
            CpuPercent = snap.CpuPercent;
            RamPercent = snap.RamPercent;
            DiskPercent = snap.DiskActivePercent;
            StorageFreePercent = snap.StorageFreePercent;
            StartupCount = snap.StartupCount;

            var f = hs.Factors;
            CpuStatus = f.FirstOrDefault(x => x.Factor == Domain.Enums.HealthFactor.Cpu)?.Status ?? "—";
            RamStatus = f.FirstOrDefault(x => x.Factor == Domain.Enums.HealthFactor.Memory)?.Status ?? "—";
            StorageStatus = f.FirstOrDefault(x => x.Factor == Domain.Enums.HealthFactor.Storage)?.Status ?? "—";

            Actions = _health.GetRecommendations(hs, snap);
            LastScanText = $"Last scan: {DateTime.Now:HH:mm:ss}";
            Log.Information("Dashboard loaded score {Score}", hs.Overall);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Dashboard load failed");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();
}
