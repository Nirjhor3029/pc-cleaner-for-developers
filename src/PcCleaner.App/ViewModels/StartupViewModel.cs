using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using System.Collections.ObjectModel;

namespace PcCleaner.App.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly IStartupManager _manager;

    [ObservableProperty] private ObservableCollection<StartupEntry> entries = new();
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string statusText = "Loading...";
    [ObservableProperty] private StartupEntry? selected;

    public StartupViewModel(IStartupManager manager) => _manager = manager;

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true; StatusText = "Scanning startup items...";
        try
        {
            var list = await _manager.GetAsync();
            Entries = new ObservableCollection<StartupEntry>(list);
            StatusText = $"{list.Count} startup items found. {list.Count(x => x.Impact == Domain.Enums.StartupImpact.High)} high impact.";
        }
        catch (Exception ex) { StatusText = $"Failed: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public async Task DisableAsync(StartupEntry? entry)
    {
        var e = entry ?? Selected;
        if (e == null) return;
        if (!e.IsSafeToDisable) return;
        var ok = await _manager.DisableAsync(e.Id);
        if (ok) await LoadAsync();
    }

    [RelayCommand]
    public async Task EnableAsync(StartupEntry? entry)
    {
        var e = entry ?? Selected;
        if (e == null) return;
        var ok = await _manager.EnableAsync(e.Id);
        if (ok) await LoadAsync();
    }
}
