using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PcCleaner.Domain.Enums;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.App;

/// <summary>One System Tools card (e.g. Hibernation) with its options.</summary>
public class TweakViewModel : ObservableObject
{
    private string _statusText = "Checking";
    private string _details = string.Empty;
    private string? _currentOptionId;
    private string _message = string.Empty;
    private bool _messageIsError;
    private bool _isBusy;

    public TweakViewModel(ISystemTweak tweak)
    {
        Tweak = tweak;
        Options = new ObservableCollection<TweakOptionViewModel>(
            tweak.Options.Select(o => new TweakOptionViewModel(this, o)));
    }

    public ISystemTweak Tweak { get; }
    public string Title => Tweak.Title;
    public string Summary => Tweak.Summary;
    public string IconGlyph => Tweak.IconGlyph;
    public string GroupName => "tweak-" + Tweak.Id;
    public ObservableCollection<TweakOptionViewModel> Options { get; }

    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }
    public string Details { get => _details; private set => SetProperty(ref _details, value); }

    public string Message
    {
        get => _message;
        set
        {
            SetProperty(ref _message, value);
            OnPropertyChanged(nameof(MessageVisibility));
        }
    }

    public bool MessageIsError { get => _messageIsError; set => SetProperty(ref _messageIsError, value); }
    public Visibility MessageVisibility => string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            SetProperty(ref _isBusy, value);
            OnPropertyChanged(nameof(CanApply));
        }
    }

    public TweakOptionViewModel? SelectedOption => Options.FirstOrDefault(o => o.IsSelected);

    /// <summary>Apply is only offered when a different option than the current state is picked.</summary>
    public bool CanApply => !IsBusy && SelectedOption != null && SelectedOption.Id != _currentOptionId;

    public void ApplyState(TweakState state)
    {
        _currentOptionId = state.CurrentOptionId;
        StatusText = state.StatusText;
        Details = state.Details;
        foreach (var option in Options)
        {
            option.IsCurrent = option.Id == state.CurrentOptionId;
            option.Hint = state.OptionHints != null && state.OptionHints.TryGetValue(option.Id, out var hint) ? hint : string.Empty;
            option.IsSelected = option.IsCurrent;
        }
        OnPropertyChanged(nameof(CanApply));
    }

    internal void SelectionChanged() => OnPropertyChanged(nameof(CanApply));
}

public class TweakOptionViewModel : ObservableObject
{
    private readonly TweakViewModel _owner;
    private bool _isSelected;
    private bool _isCurrent;
    private string _hint = string.Empty;

    public TweakOptionViewModel(TweakViewModel owner, TweakOption option)
    {
        _owner = owner;
        Option = option;
    }

    public TweakOption Option { get; }
    public string Id => Option.Id;
    public string Title => Option.Title;
    public string Description => Option.Description;
    public string Badge => Option.Badge ?? string.Empty;
    public Visibility BadgeVisibility => string.IsNullOrEmpty(Option.Badge) ? Visibility.Collapsed : Visibility.Visible;
    public string GroupName => _owner.GroupName;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
                _owner.SelectionChanged();
        }
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            SetProperty(ref _isCurrent, value);
            OnPropertyChanged(nameof(CurrentVisibility));
        }
    }

    public Visibility CurrentVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;

    public string Hint { get => _hint; set => SetProperty(ref _hint, value); }
}

public class StartupEntryViewModel : ObservableObject
{
    private bool _isEnabled;

    public StartupEntryViewModel(StartupEntry entry, bool isAdmin)
    {
        Entry = entry;
        _isEnabled = entry.Status == StartupStatus.Enabled;
        NeedsAdmin = entry.RequiresAdmin && !isAdmin;
        CanToggle = !NeedsAdmin && (entry.IsSafeToDisable || !_isEnabled);
    }

    public StartupEntry Entry { get; }
    /// <summary>Prefers the program's own name over registry IDs like "GoogleChromeAutoLaunch_6BC0…".</summary>
    public string Name => !string.IsNullOrEmpty(Entry.Description)
        ? Entry.Description
        : Entry.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? Entry.Name[..^4] : Entry.Name;
    public string Publisher => string.IsNullOrEmpty(Entry.Publisher) ? "Unknown publisher" : Entry.Publisher;

    public string ImpactText => Entry.Impact switch
    {
        StartupImpact.High => "High startup impact",
        StartupImpact.Low => "Low startup impact",
        StartupImpact.Medium => "Medium startup impact",
        _ => string.Empty
    };

    public bool IsHighImpact => Entry.Impact == StartupImpact.High;

    public string LocationText => Entry.Registration switch
    {
        StartupLocation.UserRegistry => "Registry, this user",
        StartupLocation.MachineRegistry => "Registry, all users",
        StartupLocation.MachineRegistry32 => "Registry, all users (32-bit)",
        StartupLocation.UserStartupFolder => "Startup folder, this user",
        _ => "Startup folder, all users"
    };

    public string Command => Entry.Command;
    public bool CanToggle { get; }
    private bool NeedsAdmin { get; }

    public string ToggleToolTip => NeedsAdmin
        ? "Starts for all users. Restart PC Cleaner as administrator to change this."
        : !CanToggle
            ? "Security software. PC Cleaner keeps it starting with Windows."
            : IsEnabled ? "Starts when you sign in. Click to stop it starting automatically." : "Doesn't start automatically. Click to turn back on.";

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
                OnPropertyChanged(nameof(ToggleToolTip));
        }
    }
}
