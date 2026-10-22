using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using System.Collections.ObjectModel;

namespace PcCleaner.App.ViewModels;

public partial class ResourceViewModel : ObservableObject
{
    private readonly IResourceMonitor _monitor;
    private readonly ISystemScanner _scanner;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<ProcessSample> topCpu = new();
    [ObservableProperty] private ObservableCollection<ProcessSample> topMemory = new();
    [ObservableProperty] private double cpuPercent;
    [ObservableProperty] private double ramPercent;
    [ObservableProperty] private string statusText = "Ready";
    [ObservableProperty] private bool isMonitoring;

    public ResourceViewModel(IResourceMonitor monitor, ISystemScanner scanner)
    {
        _monitor = monitor;
        _scanner = scanner;
    }

    [RelayCommand]
    public async Task StartMonitoringAsync()
    {
        if (IsMonitoring) return;
        IsMonitoring = true; _cts = new CancellationTokenSource();
        StatusText = "Monitoring...";
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var snap = await _scanner.ScanAsync(_cts.Token);
                CpuPercent = snap.CpuPercent;
                RamPercent = snap.RamPercent;
                var cpu = await _monitor.GetTopCpuAsync(6, _cts.Token);
                var mem = await _monitor.GetTopMemoryAsync(6, _cts.Token);
                TopCpu = new ObservableCollection<ProcessSample>(cpu);
                TopMemory = new ObservableCollection<ProcessSample>(mem);
                await Task.Delay(1500, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
        finally { IsMonitoring = false; StatusText = "Monitoring stopped"; }
    }

    [RelayCommand]
    public void StopMonitoring()
    {
        _cts?.Cancel();
        IsMonitoring = false;
    }

    [RelayCommand]
    public async Task EndTaskAsync(ProcessSample? p)
    {
        if (p == null || p.IsCritical) return;
        var ok = await _monitor.TryEndProcessAsync(p.Pid, gracefulFirst: true);
        StatusText = ok ? $"Ended {p.Name}" : $"Failed to end {p.Name}";
    }
}
