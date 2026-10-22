using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PcCleaner.App.ViewModels;
using PcCleaner.App.Views;
using PcCleaner.Domain.Interfaces;

namespace PcCleaner.App;

/// <summary>
/// Main window. Overview pages (Dashboard, Resources, History) render inside the shell;
/// the tools (Cleaner, Startup apps, Disk analyzer, System tools) open their own windows.
/// </summary>
public partial class ShellWindow : Window
{
    private readonly IServiceProvider _sp;
    private RadioButton _currentPage;
    private bool _switching;

    public ShellWindow(IServiceProvider sp)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        _sp = sp;
        _currentPage = NavDashboard;
        ShowPage(NavDashboard);
    }

    /// <summary>Checked (not Click) so keyboard navigation and UI Automation work too.</summary>
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (_switching || sender is not RadioButton nav || _sp == null)
            return;

        if (nav == NavCleaner)
            OpenCleaner(startScan: false);
        else if (nav == NavStartup)
            OpenSystemTools(startupTab: true);
        else if (nav == NavAnalyzer)
            OpenDiskAnalyzer();
        else if (nav == NavTools)
            OpenSystemTools();
        else
        {
            ShowPage(nav);
            return;
        }

        // Tool windows open on top; keep the page that is actually showing selected
        Select(_currentPage);
    }

    private void Select(RadioButton nav)
    {
        _switching = true;
        nav.IsChecked = true;
        _switching = false;
    }

    private void ShowPage(RadioButton page)
    {
        _currentPage = page;
        Select(page);

        if (page == NavResource)
        {
            var vm = _sp.GetRequiredService<ResourceViewModel>();
            MainContent.Content = new ResourceView { DataContext = vm };
            _ = vm.StartMonitoringAsync();
        }
        else if (page == NavHistory)
        {
            MainContent.Content = new HistoryView();
        }
        else
        {
            var vm = _sp.GetRequiredService<DashboardViewModel>();
            MainContent.Content = new DashboardView { DataContext = vm };
            _ = LoadDashboardAsync(vm);
        }
    }

    private async Task LoadDashboardAsync(DashboardViewModel vm)
    {
        await vm.LoadAsync();
        if (vm.OverallScore > 0)
            HealthBadge.Text = $"Health score {vm.OverallScore} of 100, {vm.OverallStatus.ToLowerInvariant()}";
    }

    public void OpenCleaner(bool startScan)
    {
        var window = _sp.GetRequiredService<MainWindow>();
        window.Owner = this;
        if (startScan)
            window.Loaded += (_, _) => window.StartScan(allowElevation: false);
        window.Show();
    }

    public void OpenSystemTools(bool startupTab = false)
    {
        var window = _sp.GetRequiredService<SystemToolsWindow>();
        window.Owner = this;
        if (startupTab)
            window.ShowStartupTab();
        window.Show();
    }

    private void OpenDiskAnalyzer()
    {
        var window = new DiskAnalyzerWindow(_sp.GetRequiredService<IDiskAnalyzer>()) { Owner = this };
        window.Show();
    }
}
