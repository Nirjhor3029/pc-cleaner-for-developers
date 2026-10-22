using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using PcCleaner.Domain.Interfaces;
using PcCleaner.Domain.Models;

namespace PcCleaner.App;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

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

    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string Details { get => _details; private set => Set(ref _details, value); }

    public string Message
    {
        get => _message;
        set
        {
            Set(ref _message, value);
            OnPropertyChanged(nameof(MessageVisibility));
        }
    }

    public bool MessageIsError { get => _messageIsError; set => Set(ref _messageIsError, value); }
    public Visibility MessageVisibility => string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            Set(ref _isBusy, value);
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
            if (Set(ref _isSelected, value))
                _owner.SelectionChanged();
        }
    }

    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            Set(ref _isCurrent, value);
            OnPropertyChanged(nameof(CurrentVisibility));
        }
    }

    public Visibility CurrentVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;

    public string Hint { get => _hint; set => Set(ref _hint, value); }
}

public class StartupEntryViewModel : ObservableObject
{
    private bool _isEnabled;

    public StartupEntryViewModel(StartupEntry entry, bool isAdmin)
    {
        Entry = entry;
        _isEnabled = entry.IsEnabled;
        CanToggle = !entry.RequiresAdmin || isAdmin;
    }

    public StartupEntry Entry { get; }
    /// <summary>Prefers the program's own name over registry IDs like "GoogleChromeAutoLaunch_6BC0…".</summary>
    public string Name => !string.IsNullOrEmpty(Entry.Description)
        ? Entry.Description
        : Entry.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? Entry.Name[..^4] : Entry.Name;
    public string Publisher => string.IsNullOrEmpty(Entry.Publisher) ? "Unknown publisher" : Entry.Publisher;

    public string LocationText => Entry.Location switch
    {
        StartupLocation.UserRegistry => "Registry, this user",
        StartupLocation.MachineRegistry => "Registry, all users",
        StartupLocation.MachineRegistry32 => "Registry, all users (32-bit)",
        StartupLocation.UserStartupFolder => "Startup folder, this user",
        _ => "Startup folder, all users"
    };

    public string Command => Entry.Command;
    public bool CanToggle { get; }

    public string ToggleToolTip => CanToggle
        ? (IsEnabled ? "Starts when you sign in. Click to stop it starting automatically." : "Doesn't start automatically. Click to turn back on.")
        : "Starts for all users. Restart PC Cleaner as administrator to change this.";

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (Set(ref _isEnabled, value))
                OnPropertyChanged(nameof(ToggleToolTip));
        }
    }
}
