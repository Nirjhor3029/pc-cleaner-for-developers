using System.Windows;
using System.Windows.Controls;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Infrastructure.SystemTools;
using PcCleaner.Infrastructure.Windows;
using Serilog;

namespace PcCleaner.App;

public partial class SystemToolsWindow : Window
{
    private readonly IStartupManager _startupManager;
    private readonly RestorePointCreator _restorePoints;
    private readonly List<TweakViewModel> _tweaks;
    private readonly bool _isAdmin = AdminHelper.IsRunningAsAdmin();
    private List<StartupEntryViewModel> _startupEntries = new();

    public SystemToolsWindow(IEnumerable<ISystemTweak> tweaks, IStartupManager startupManager, RestorePointCreator restorePoints)
    {
        InitializeComponent();
        DarkTitleBar.Enable(this);
        _startupManager = startupManager;
        _restorePoints = restorePoints;
        _tweaks = tweaks.Select(t => new TweakViewModel(t)).ToList();

        TweaksList.ItemsSource = _tweaks.Where(t => t.Tweak.Category == "Power & Storage").ToList();
        AdminBanner.Visibility = _isAdmin ? Visibility.Collapsed : Visibility.Visible;
        Loaded += async (_, _) =>
        {
            await RefreshTweaksAsync();
            await LoadStartupAsync();
        };
    }

    public void ShowStartupTab() => NavStartup.IsChecked = true;

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (PowerPanel == null || StartupPanel == null)
            return; // fired during InitializeComponent

        PowerPanel.Visibility = NavPower.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        StartupPanel.Visibility = NavStartup.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshTweaksAsync()
    {
        foreach (var vm in _tweaks)
        {
            try
            {
                vm.ApplyState(await vm.Tweak.GetStateAsync());
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not read state of {Tweak}", vm.Tweak.Id);
                vm.Message = $"Couldn't read the current setting: {ex.Message}";
                vm.MessageIsError = true;
            }
        }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TweakViewModel vm || vm.SelectedOption is not { } option)
            return;

        if (vm.Tweak.RequiresAdmin && !_isAdmin)
        {
            OfferRestartAsAdmin($"Changing {vm.Title} needs administrator rights.");
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Set {vm.Title} to {option.Title}?\n\n{option.Description}",
            $"Change {vm.Title}", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        vm.IsBusy = true;
        vm.Message = "Applying";
        vm.MessageIsError = false;
        try
        {
            Log.Information("Applying {Tweak} = {Option}", vm.Tweak.Id, option.Id);
            var result = await vm.Tweak.ApplyAsync(option.Id);
            vm.Message = result.Message;
            vm.MessageIsError = !result.Success;
            vm.ApplyState(await vm.Tweak.GetStateAsync());
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Applying {Tweak} failed", vm.Tweak.Id);
            vm.Message = ex.Message;
            vm.MessageIsError = true;
        }
        finally
        {
            vm.IsBusy = false;
        }
    }

    private async Task LoadStartupAsync()
    {
        StartupError.Text = string.Empty;
        try
        {
            var entries = await _startupManager.GetAsync();
            _startupEntries = entries.Select(e => new StartupEntryViewModel(e, _isAdmin)).ToList();
            StartupList.ItemsSource = _startupEntries;
            UpdateStartupSummary();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reading startup entries failed");
            StartupError.Text = $"Couldn't read startup apps: {ex.Message}";
        }
    }

    private void UpdateStartupSummary()
    {
        int on = _startupEntries.Count(e => e.IsEnabled);
        StartupSummary.Text = $"{on} of {_startupEntries.Count} apps open by themselves when you sign in.";
    }

    private async void RefreshStartup_Click(object sender, RoutedEventArgs e) => await LoadStartupAsync();

    private async void StartupToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox toggle || toggle.DataContext is not StartupEntryViewModel vm)
            return;

        bool enable = toggle.IsChecked == true;
        try
        {
            bool changed = enable
                ? await _startupManager.EnableAsync(vm.Entry.Id)
                : await _startupManager.DisableAsync(vm.Entry.Id);
            if (!changed)
                throw new InvalidOperationException("the entry no longer exists or is protected. Refresh and try again.");
            vm.IsEnabled = enable;
            Log.Information("Startup entry {Name} ({Location}) set to {State}", vm.Entry.Name, vm.Entry.Registration, enable ? "enabled" : "disabled");
            StartupError.Text = string.Empty;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Changing startup entry {Name} failed", vm.Entry.Name);
            toggle.IsChecked = vm.IsEnabled;
            StartupError.Text = $"Couldn't change {vm.Name}: {ex.Message}";
        }
        UpdateStartupSummary();
    }

    private async void RestorePointButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isAdmin)
        {
            OfferRestartAsAdmin("Creating a restore point needs administrator rights.");
            return;
        }

        RestorePointButton.IsEnabled = false;
        RestorePointStatus.Foreground = (System.Windows.Media.Brush)FindResource("FgMutedBrush");
        RestorePointStatus.Text = "Creating restore point. This can take a minute.";
        var (success, message) = await _restorePoints.CreateAsync();
        RestorePointStatus.Foreground = (System.Windows.Media.Brush)FindResource(success ? "AccentBrush" : "DangerBrush");
        RestorePointStatus.Text = message;
        RestorePointButton.IsEnabled = true;
    }

    private void RestartAsAdmin_Click(object sender, RoutedEventArgs e) => RestartAsAdmin();

    private void OfferRestartAsAdmin(string reason)
    {
        var answer = MessageBox.Show(this, $"{reason}\n\nRestart PC Cleaner as administrator and come back here?",
            "Administrator rights needed", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes)
            RestartAsAdmin();
    }

    private void RestartAsAdmin()
    {
        if (AdminHelper.Elevate(App.OpenToolsArgument))
            System.Windows.Application.Current.Shutdown();
    }
}
