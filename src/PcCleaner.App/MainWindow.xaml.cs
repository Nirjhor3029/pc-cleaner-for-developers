using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using PcCleaner.Application.Interfaces;
using PcCleaner.Application.Models;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;
using PcCleaner.Infrastructure.Windows;
using Serilog;

namespace PcCleaner.App;

public partial class MainWindow : Window
{
    private readonly IScanService _scanService;
    private readonly ICleaner _cleaner;
    private readonly IDiskAnalyzer _diskAnalyzer;
    private List<ScanResult>? _scanResults;
    private List<ScanResultViewModel> _scanViewModels = new();

    public MainWindow(IScanService scanService, ICleaner cleaner, IDiskAnalyzer diskAnalyzer)
    {
        InitializeComponent();
        _scanService = scanService;
        _cleaner = cleaner;
        _diskAnalyzer = diskAnalyzer;
    }

    private void DiskButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new DiskAnalyzerWindow(_diskAnalyzer)
        {
            Owner = this
        };
        window.Show();
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        ScanButton.IsEnabled = false;
        ScanButton.Content = "Scanning...";
        StatusText.Text = "Scanning...";
        ResultsList.Items.Clear();
        ResultsList.ItemsSource = null;
        EmptyHint.Visibility = Visibility.Visible;
        CleanButton.IsEnabled = false;
        TotalSizeText.Text = "0 GB";
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;

        _ = PerformScanAsync();
    }

    private async Task PerformScanAsync()
    {
        Log.Information("Scan started");
        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                ProgressBar.Value = p.Percent;
                StatusText.Text = $"Scanning... {p.CategoryName} ({p.CurrentIndex}/{p.Total})";
            });

            _scanResults = await _scanService.RunAllScannersAsync(progress);

            long totalSize = 0;
            var viewModels = new List<ScanResultViewModel>();
            foreach (var result in _scanResults)
            {
                totalSize += result.TotalSizeBytes;
                var vm = new ScanResultViewModel
                {
                    Name = result.ScannerName,
                    Type = result.Type,
                    SizeBytes = result.TotalSizeBytes,
                    ItemCount = result.ItemCount,
                    IsSelected = true
                };

                if (result.Type == ScannerType.PlaywrightCache)
                {
                    vm.IsSelected = false;
                    vm.IsWarning = true;
                    vm.WarningText = "⚠ Re-downloaded on next run";
                }

                viewModels.Add(vm);
            }

            _scanViewModels = viewModels;
            foreach (var vm in _scanViewModels)
                vm.PropertyChanged += OnViewModelPropertyChanged;

            var grouped = CollectionViewSource.GetDefaultView(_scanViewModels);
            grouped.GroupDescriptions.Clear();
            grouped.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ScanResultViewModel.SectionTitle)));

            TotalSizeText.Text = FormatSize(SelectedSizeBytes());
            ResultsList.ItemsSource = grouped;
            EmptyHint.Visibility = _scanResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = $"Scan complete - {_scanResults.Count} categories, found {FormatSize(totalSize)}";
            Log.Information("Scan complete: {Categories} categories, total {Size:N0} bytes",
                _scanResults.Count, totalSize);
            CleanButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Scan failed");
            StatusText.Text = $"Scan failed: {ex.Message}";
        }
        finally
        {
            ProgressBar.Visibility = Visibility.Collapsed;
            SetBusy(false);
            ScanButton.IsEnabled = true;
            ScanButton.Content = "SCAN";
        }
    }

    private async void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResults == null)
            return;

        var selectedItems = new List<ScanItem>();
        long totalSelected = 0;
        bool adminCategorySelected = false;

        for (int i = 0; i < _scanResults.Count; i++)
        {
            var result = _scanResults[i];
            var vm = _scanViewModels.FirstOrDefault(v => v.Type == result.Type);
            if (vm == null || !vm.IsSelected)
                continue;

            if (IsAdminCategory(result.Type))
                adminCategorySelected = true;

            foreach (var item in result.Items)
            {
                selectedItems.Add(item);
            }
            totalSelected += result.TotalSizeBytes;
        }

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("No items selected to clean.", "PC Cleaner",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        Log.Information("Clean requested: {Count} items, {Size:N0} bytes",
            selectedItems.Count, totalSelected);

        var itemsToClean = selectedItems;
        if (adminCategorySelected && !AdminHelper.IsRunningAsAdmin())
        {
            var elevate = MessageBox.Show(
                "Some selected categories require administrator privileges.\n\n" +
                "Do you want to restart PC Cleaner as administrator? (Recommended)\n\n" +
                "If you choose No, only non-admin categories will be cleaned.",
                "Administrator Permission Required",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (elevate == MessageBoxResult.Yes)
            {
                if (AdminHelper.Elevate())
                {
                    Close();
                    return;
                }
                MessageBox.Show("Elevation was cancelled. No files were cleaned.",
                    "PC Cleaner", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            else if (elevate == MessageBoxResult.Cancel)
            {
                return;
            }

            itemsToClean = selectedItems
                .Where(item => !IsAdminCategory(FindCategoryType(item)))
                .ToList();

            if (itemsToClean.Count == 0)
            {
                StatusText.Text = "Admin-required categories selected - no files cleaned.";
                MessageBox.Show(
                    "All selected categories require administrator privileges.\n" +
                    "Please run PC Cleaner as administrator to clean them.",
                    "PC Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox.Show(
                $"Cleaning {itemsToClean.Count:N0} files from non-admin categories only.\n" +
                "Admin-required categories were skipped.",
                "PC Cleaner", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        var confirm = MessageBox.Show(
            $"Clean {FormatSize(itemsToClean.Sum(i => i.SizeBytes))} from {itemsToClean.Count:N0} files?",
            "Confirm Cleaning", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
            return;

        try
        {
            SetBusy(true);
            CleanButton.IsEnabled = false;
            StatusText.Text = "Cleaning...";
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;

            var progress = new Progress<CleanUpProgress>(p =>
            {
                ProgressBar.Value = p.Percent;
                if (p.TotalFiles > 0)
                {
                    StatusText.Text = $"Cleaning... {p.FilesProcessed:N0} of {p.TotalFiles:N0} files ({p.Percent:0}%)";
                }
                else
                {
                    StatusText.Text = "Cleaning... ";
                }
            });

            var result = await Task.Run(() => _cleaner.CleanAsync(itemsToClean, progress));

            StatusText.Text = $"Cleaned {FormatSize(result.RemovedBytes)}, skipped {result.FilesSkipped} files - click SCAN to rescan";

            Log.Information("Cleaning complete: removed={Removed}, skipped={Skipped}, skippedFiles={SkippedFiles}",
                FormatSize(result.RemovedBytes), FormatSize(result.SkippedBytes), result.FilesSkipped);

            MessageBox.Show(
                $"Found:     {FormatSize(result.FoundBytes)}\n" +
                $"Removed:   {FormatSize(result.RemovedBytes)}\n" +
                $"Skipped:   {FormatSize(result.SkippedBytes)}\n\n" +
                $"Files deleted: {result.FilesDeleted:N0}\n" +
                $"Files skipped: {result.FilesSkipped:N0}\n\n" +
                (result.SkippedReasons.Count > 0
                    ? $"Note: {result.FilesSkipped} files were skipped (in use, not found, or no permission)."
                    : "All selected items were cleaned successfully."),
                "Cleaning Complete", MessageBoxButton.OK, MessageBoxImage.Information);

            _scanResults = null;
            CleanButton.IsEnabled = false;
            SetBusy(false);
            ProgressBar.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cleaning failed");
            StatusText.Text = $"Cleaning failed: {ex.Message}";
            MessageBox.Show($"An error occurred: {ex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
            CleanButton.IsEnabled = true;
            SetBusy(false);
            ProgressBar.Visibility = Visibility.Collapsed;
        }
    }

    private void SetBusy(bool busy)
    {
        ScanButton.IsEnabled = !busy;
        DiskButton.IsEnabled = !busy;
        ResultsList.IsEnabled = !busy;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScanResultViewModel.IsSelected))
            return;

        TotalSizeText.Text = FormatSize(SelectedSizeBytes());
    }

    private long SelectedSizeBytes()
    {
        return _scanViewModels.Where(v => v.IsSelected).Sum(v => v.SizeBytes);
    }

    private static bool IsAdminCategory(ScannerType type)
    {
        return type is ScannerType.WindowsTemp
            || type is ScannerType.WindowsUpdateCache
            || type is ScannerType.DeliveryOptimization;
    }

    private ScannerType FindCategoryType(ScanItem item)
    {
        if (_scanResults == null)
            return ScannerType.UserTemp;

        foreach (var result in _scanResults)
        {
            if (result.Items.Any(i => i.FilePath == item.FilePath))
                return result.Type;
        }

        return ScannerType.UserTemp;
    }

    private string FormatSize(long bytes)
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