using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PcCleaner.Domain.Enums;

namespace PcCleaner.App;

public class ScanResultViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public string Name { get; set; } = string.Empty;
    public ScannerType Type { get; set; }
    public long SizeBytes { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsWarning { get; set; }
    public string WarningText { get; set; } = string.Empty;
    public int ItemCount { get; set; }

    public CleanupSection Section => Type.Section();

    public string SectionTitle => Section == CleanupSection.Developer
        ? "Developer Cleanup"
        : "System Cleanup";

    public Visibility WarningVisibility => IsWarning ? Visibility.Visible : Visibility.Collapsed;

    public string Display => $"{Name}  ({ItemCount:N0} files)";

    public string SizeDisplay => FormatSize(SizeBytes);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}