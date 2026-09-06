using System.IO;
using System.Windows;
using System.Windows.Input;
using PcCleaner.Domain.Interfaces;

namespace PcCleaner.App;

public partial class DiskAnalyzerWindow : Window
{
    private readonly IDiskAnalyzer _analyzer;
    private readonly Stack<string> _history = new();
    private string _currentPath = string.Empty;
    private CancellationTokenSource? _cts;

    public DiskAnalyzerWindow(IDiskAnalyzer analyzer)
    {
        InitializeComponent();
        _analyzer = analyzer;
        LoadDrives();
    }

    private void LoadDrives()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(d => d.Name)
            .ToList();

        DriveCombo.ItemsSource = drives;
        if (drives.Count > 0)
            DriveCombo.SelectedItem = drives.FirstOrDefault(d => d.StartsWith("C")) ?? drives[0];
    }

    private async void DriveCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DriveCombo.SelectedItem is not string drive)
            return;

        _history.Clear();
        BackButton.IsEnabled = false;
        await NavigateAsync(drive);
    }

    private async void RescanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentPath))
            await NavigateAsync(_currentPath);
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count == 0)
            return;

        var parent = _history.Pop();
        BackButton.IsEnabled = _history.Count > 0;
        await NavigateAsync(parent);
    }

    private async void EntryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntryList.SelectedItem is not DiskEntryViewModel vm || !vm.IsDirectory)
            return;

        _history.Push(_currentPath);
        BackButton.IsEnabled = true;
        await NavigateAsync(vm.FullPath);
    }

    private async Task NavigateAsync(string path)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _currentPath = path;
        PathText.Text = path;
        StatusText.Text = "Scanning folder sizes... (may take a moment)";
        EntryList.ItemsSource = null;

        try
        {
            var entries = await _analyzer.GetEntriesAsync(path, token);

            long max = entries.Count > 0 ? entries.Max(e => e.SizeBytes) : 0;
            var viewModels = entries.Select(e =>
            {
                var vm = new DiskEntryViewModel(e);
                vm.BarPercent = max > 0 ? Math.Round(e.SizeBytes * 100.0 / max, 1) : 0;
                return vm;
            }).ToList();

            EntryList.ItemsSource = viewModels;

            long shownBytes = viewModels.Sum(v => v.SizeBytes);
            var drive = GetDriveOf(path);
            if (drive is { IsReady: true })
            {
                double used = drive.TotalSize - drive.TotalFreeSpace;
                double total = drive.TotalSize;
                double pct = total > 0 ? used * 100.0 / total : 0;
                StatusText.Text =
                    $"Items: {entries.Count} · Folders total: {DiskEntryViewModel.FormatSize(shownBytes)} · " +
                    $"Drive {drive.Name} used: {FormatGb(used)} of {FormatGb(total)} ({pct:0.0}%) · " +
                    (entries.Count > 0 ? $"Largest: {viewModels[0].Name} ({viewModels[0].SizeDisplay})" : "");
            }
            else
            {
                StatusText.Text = $"Items: {entries.Count} · Total: {DiskEntryViewModel.FormatSize(shownBytes)} · " +
                                  (entries.Count > 0 ? $"Largest: {viewModels[0].Name} ({viewModels[0].SizeDisplay})" : "-");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Scan failed: {ex.Message}";
        }
    }

    private static DriveInfo? GetDriveOf(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return string.IsNullOrEmpty(root) ? null : new DriveInfo(root);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatGb(double bytes)
    {
        return $"{bytes / 1024.0 / 1024.0 / 1024.0:0.0} GB";
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnClosed(e);
    }
}