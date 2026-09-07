using System.Windows;
using System.Windows.Controls;
using PcCleaner.App.Views;
using PcCleaner.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace PcCleaner.App;

public partial class ShellWindow : Window
{
    private readonly IServiceProvider _sp;
    private string _current = "Dashboard";

    public ShellWindow(IServiceProvider sp)
    {
        InitializeComponent();
        _sp = sp;
        Navigate("Dashboard");
        Loaded += async (_, _) =>
        {
            // Auto-load dashboard data
            if (MainContent.Content is DashboardView dv && dv.DataContext is DashboardViewModel vm)
                await vm.LoadAsync();
        };
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string tag) Navigate(tag);
    }

    private void Navigate(string tag)
    {
        _current = tag;
        UpdateNavStyles(tag);

        object view = tag switch
        {
            "Dashboard" => new DashboardView { DataContext = _sp.GetRequiredService<DashboardViewModel>() },
            "Cleaner" => CreateCleanerView(),
            "Startup" => new StartupView { DataContext = _sp.GetRequiredService<StartupViewModel>() },
            "Resource" => new ResourceView { DataContext = _sp.GetRequiredService<ResourceViewModel>() },
            "Analyzer" => CreateAnalyzerHost(),
            "History" => new HistoryView(),
            _ => new TextBlock { Text = tag, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(20) }
        };

        MainContent.Content = view;

        // Auto-load for startup/resource
        if (view is StartupView sv && sv.DataContext is StartupViewModel svm) _ = svm.LoadAsync();
        if (view is ResourceView rv && rv.DataContext is ResourceViewModel rvm) _ = rvm.StartMonitoringAsync();
        if (view is DashboardView dv2 && dv2.DataContext is DashboardViewModel dvm) _ = dvm.LoadAsync();
    }

    private object CreateCleanerView()
    {
        // Host existing MainWindow's UI as embedded view: create a Frame with MainWindow content
        // Simplest: create a UserControl that wraps MainWindow logic; for MVP reuse MainWindow as separate window host
        var host = new Border { Background = System.Windows.Media.Brushes.Transparent };
        var btn = new Button
        {
            Content = "Open Cleaner (Classic 400x600)",
            Style = (Style)FindResource("MainButtonStyle"),
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e94560")),
            Width = 280, Margin = new Thickness(20)
        };
        btn.Click += (_, _) =>
        {
            var w = _sp.GetRequiredService<MainWindow>();
            w.Show();
        };
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = "Cleaner — Expanded", Foreground = System.Windows.Media.Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0,10,0,10) });
        stack.Children.Add(new TextBlock { Text = "Existing cleaner (26 categories) + Firefox support + Prefetch optional. Click to open classic cleaner window (preserved for safety).", Foreground = System.Windows.Media.Brushes.Gray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) });
        stack.Children.Add(btn);
        stack.Children.Add(new TextBlock { Text = "Next iteration will embed cleaner list directly here with Safety badges ☑ Safe / ☐ Caution.", Foreground = System.Windows.Media.Brushes.Gray, FontSize = 11, Margin = new Thickness(0,12,0,0) });
        host.Child = stack;
        return host;
    }

    private object CreateAnalyzerHost()
    {
        var host = new Border();
        var stack = new StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new TextBlock { Text = "Disk Analyzer", Foreground = System.Windows.Media.Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold });
        var btn = new Button { Content = "Open Disk Analyzer ▸", Style = (Style)FindResource("MainButtonStyle"), Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#533483")), Width = 220, Margin = new Thickness(0,16,0,0) };
        btn.Click += (_, _) =>
        {
            var svc = _sp.GetRequiredService<PcCleaner.Domain.Interfaces.IDiskAnalyzer>();
            var w = new DiskAnalyzerWindow(svc) { Owner = this };
            w.Show();
        };
        stack.Children.Add(btn);
        host.Child = stack;
        return host;
    }

    private void UpdateNavStyles(string active)
    {
        var map = new Dictionary<string, Button>
        {
            ["Dashboard"] = NavDashboard,
            ["Cleaner"] = NavCleaner,
            ["Startup"] = NavStartup,
            ["Resource"] = NavResource,
            ["Analyzer"] = NavAnalyzer,
            ["History"] = NavHistory
        };
        foreach (var kv in map)
        {
            kv.Value.Style = kv.Key == active ? (Style)FindResource("NavActiveButtonStyle") : (Style)FindResource("NavButtonStyle");
        }

        // Update badge
        try
        {
            if (_sp.GetService(typeof(DashboardViewModel)) is DashboardViewModel vm && vm.OverallScore > 0)
                HealthBadge.Text = $"Score {vm.OverallScore}/100 · {vm.OverallStatus}";
        }
        catch { }
    }
}
