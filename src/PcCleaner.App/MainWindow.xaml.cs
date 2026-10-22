using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
        DarkTitleBar.Enable(this);
        _scanService = scanService;
        _cleaner = cleaner;
        _diskAnalyzer = diskAnalyzer;
        AdminBadge.Visibility = AdminHelper.IsRunningAsAdmin() ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) => UpdateGauge(animate: false);
    }

    private void SystemToolsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSystemTools();
    }

    public void OpenSystemTools()
    {
        var window = App.Services.GetService(typeof(SystemToolsWindow)) as SystemToolsWindow;
        if (window == null)
            return;
        window.Owner = this;
        window.Closed += (_, _) => UpdateGauge(animate: false); // e.g. hibernation turned off frees space
        window.Show();
    }

    /// <summary>
    /// Redraws the system-drive capacity bar: used space, the part currently selected
    /// to clean (taken out of "used"), and free space.
    /// </summary>
    private void UpdateGauge(bool animate)
    {
        DriveInfo drive;
        try
        {
            drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\");
            if (!drive.IsReady)
                return;
        }
        catch
        {
            return;
        }

        long total = drive.TotalSize;
        long free = drive.TotalFreeSpace;
        long used = total - free;
        long selected = Math.Min(SelectedSizeBytes(), used);

        DriveTitle.Text = $"{drive.Name.TrimEnd('\\')} drive";
        DriveFreeText.Text = $"{FormatSize(free)} free of {FormatSize(total)}";
        LegendUsed.Text = $"Used {FormatSize(used - selected)}";
        LegendClean.Text = $"Selected to clean {FormatSize(selected)}";
        LegendFree.Text = $"Free {FormatSize(free)}";

        GaugeUsedCol.Width = new GridLength(used - selected, GridUnitType.Star);
        GaugeCleanCol.Width = new GridLength(selected, GridUnitType.Star);
        GaugeFreeCol.Width = new GridLength(free, GridUnitType.Star);

        if (animate && selected > 0 && SystemParameters.ClientAreaAnimation)
        {
            GaugeCleanScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                });
        }
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
        StartScan(allowElevation: true);
    }

    /// <summary>
    /// Scans as administrator so admin-only categories (Windows Temp, Update cache, …)
    /// are found and can be cleaned without a second elevation + rescan. If not elevated,
    /// restarts elevated with --scan; if the UAC prompt is declined, scans without admin.
    /// </summary>
    public void StartScan(bool allowElevation)
    {
        bool elevationDeclined = false;
        if (allowElevation && !AdminHelper.IsRunningAsAdmin())
        {
            if (AdminHelper.Elevate(App.AutoScanArgument))
            {
                Close();
                return;
            }
            elevationDeclined = true;
        }

        SetBusy(true);
        ScanButton.IsEnabled = false;
        ScanButton.Content = "Scanning";
        StatusText.Text = "Scanning...";
        ResultsList.ItemsSource = null;
        _scanViewModels = new();
        EmptyHint.Text = "Scanning";
        EmptyState.Visibility = Visibility.Visible;
        CleanButton.IsEnabled = false;
        TotalSizeText.Text = "0 B";
        UpdateGauge(animate: false);
        ProgressBar.Visibility = Visibility.Visible;
        ProgressBar.Value = 0;

        _ = PerformScanAsync(elevationDeclined);
    }

    private async Task PerformScanAsync(bool elevationDeclined)
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
                // Hide categories with nothing to clean (tool not installed, cache already empty)
                if (result.ItemCount == 0)
                    continue;

                totalSize += result.TotalSizeBytes;
                var vm = new ScanResultViewModel
                {
                    Name = result.ScannerName,
                    Type = result.Type,
                    SizeBytes = result.TotalSizeBytes,
                    ItemCount = result.ItemCount,
                    IsCommandBased = result.Items.All(i => i.Command != null),
                    SizeUnknown = result.SizeUnknown,
                    IsSelected = result.SelectedByDefault,
                    IsWarning = !string.IsNullOrEmpty(result.Warning),
                    WarningText = result.Warning ?? string.Empty
                };

                if (result.Items.All(i => !i.IsDeletable))
                {
                    // e.g. browser caches while the browser is running
                    vm.IsSelected = false;
                    vm.IsWarning = true;
                    vm.WarningText = "The app is running. Close it and scan again to clean this.";
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
            EmptyState.Visibility = _scanViewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_scanViewModels.Count == 0)
                EmptyHint.Text = "Nothing to clean";
            StatusText.Text = $"Found {FormatSize(totalSize)} in {_scanViewModels.Count} categories." +
                (elevationDeclined ? " Scanned without admin, so some system items were skipped." : string.Empty);
            UpdateGauge(animate: true);
            Log.Information("Scan complete: {Categories} categories, total {Size:N0} bytes",
                _scanViewModels.Count, totalSize);
            CleanButton.IsEnabled = _scanViewModels.Count > 0;
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
            ScanButton.Content = "Scan";
        }
    }

    private async void CleanButton_Click(object sender, RoutedEventArgs e)
    {
        if (_scanResults == null)
            return;

        var selectedTypes = _scanViewModels.Where(v => v.IsSelected).Select(v => v.Type).ToHashSet();
        var selectedResults = _scanResults.Where(r => selectedTypes.Contains(r.Type)).ToList();
        var selectedItems = selectedResults.SelectMany(r => r.Items).ToList();
        long totalSelected = selectedResults.Sum(r => r.TotalSizeBytes);
        bool adminCategorySelected = selectedResults.Any(r => r.RequiresAdmin);

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

            itemsToClean = selectedResults
                .Where(r => !r.RequiresAdmin)
                .SelectMany(r => r.Items)
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
            $"Clean {FormatSize(itemsToClean.Sum(i => i.SizeBytes))} from {itemsToClean.Count:N0} items?" +
            (itemsToClean.Any(i => i.Command != null)
                ? "\n\nSystem tools (Docker / Disk Cleanup / DISM) will run and may take several minutes."
                : string.Empty),
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

            StatusText.Text = $"Cleaned {FormatSize(result.RemovedBytes)}. Skipped {result.FilesSkipped:N0} items that were in use or protected.";

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
            _scanViewModels = new();
            ResultsList.ItemsSource = null;
            EmptyHint.Text = $"Cleaned {FormatSize(result.RemovedBytes)}";
            EmptyState.Visibility = Visibility.Visible;
            TotalSizeText.Text = "0 B";
            UpdateGauge(animate: false);
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
        SystemToolsButton.IsEnabled = !busy;
        ResultsList.IsEnabled = !busy;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ScanResultViewModel.IsSelected))
            return;

        TotalSizeText.Text = FormatSize(SelectedSizeBytes());
        UpdateGauge(animate: false);
    }

    private long SelectedSizeBytes()
    {
        return _scanViewModels.Where(v => v.IsSelected).Sum(v => v.SizeBytes);
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