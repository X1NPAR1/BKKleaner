using System.Collections.ObjectModel;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Optimization;
using BKKleaner.Recovery;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class ActionItemViewModel : ObservableObject
{
    private readonly OptimizationViewModel _parent;
    public OptimizationAction Action { get; }

    [ObservableProperty] private bool _isApplied;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _previewText;

    public string Title => Loc.Instance[Action.TitleKey];
    public string Description => Loc.Instance[Action.DescriptionKey];
    public bool RequiresRestart => Action.RequiresRestart;
    public string CategoryLabel => Loc.Instance[$"opt.cat.{Action.Category}".ToLowerInvariant()];
    public string CategoryGlyph => Action.Category switch
    {
        OptimizationCategory.Power => "",
        OptimizationCategory.Gaming => "",
        OptimizationCategory.Background => "",
        OptimizationCategory.Startup => "",
        OptimizationCategory.Scheduling => "",
        OptimizationCategory.Latency => "",
        OptimizationCategory.Visual => "",
        _ => ""
    };

    public ActionItemViewModel(OptimizationAction action, OptimizationViewModel parent)
    {
        Action = action;
        _parent = parent;
        _isApplied = action.IsApplied;
    }

    [RelayCommand]
    private Task ToggleAsync() => _parent.ToggleActionAsync(this);

    [RelayCommand]
    private Task PreviewAsync() => _parent.PreviewActionAsync(this);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(CategoryLabel));
    }
}

public sealed partial class OptimizationViewModel : ObservableObject
{
    private readonly IOptimizationService _optimization;
    private readonly IRecoveryService _recovery;
    private readonly Services.INotificationService _notifications;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private int _appliedCount;

    public ObservableCollection<ActionItemViewModel> Actions { get; } = [];
    public ObservableCollection<StartupEntry> StartupEntries { get; } = [];

    /// <summary>Grouped-by-category view of the actions for the section layout.</summary>
    public System.ComponentModel.ICollectionView GroupedActions { get; }

    public OptimizationViewModel(IOptimizationService optimization, IRecoveryService recovery,
        Services.INotificationService notifications, ILocalizationService localization)
    {
        _optimization = optimization;
        _recovery = recovery;
        _notifications = notifications;
        foreach (var action in optimization.Actions)
            Actions.Add(new ActionItemViewModel(action, this));

        GroupedActions = System.Windows.Data.CollectionViewSource.GetDefaultView(Actions);
        GroupedActions.GroupDescriptions.Add(
            new System.Windows.Data.PropertyGroupDescription(nameof(ActionItemViewModel.CategoryLabel)));

        AppliedCount = Actions.Count(a => a.IsApplied);
        localization.LanguageChanged += (_, _) =>
        {
            foreach (var a in Actions) a.RefreshLocalization();
            GroupedActions.Refresh();
        };
    }

    public async Task ToggleActionAsync(ActionItemViewModel item)
    {
        if (item.IsBusy) return;
        item.IsBusy = true;
        try
        {
            if (item.Action.IsApplied)
            {
                if (await _optimization.UndoAsync(item.Action.Id))
                    item.IsApplied = false;
            }
            else
            {
                // Mandatory backup before any change.
                await _recovery.CreateFullBackupAsync(item.Action.Id);
                if (await _optimization.ApplyAsync(item.Action.Id))
                    item.IsApplied = true;
            }
            // Re-sync every toggle: applying a power plan may have reverted another one.
            foreach (var a in Actions) a.IsApplied = a.Action.IsApplied;
            AppliedCount = Actions.Count(a => a.IsApplied);

            StatusMessage = item.IsApplied
                ? $"{item.Title} ✓"
                : $"{item.Title} — {Loc.Instance["opt.reverted"]}";
            _notifications.Notify(Loc.Instance["opt.title"], StatusMessage,
                item.IsApplied ? Services.NotificationLevel.Success : Services.NotificationLevel.Info);
        }
        finally
        {
            item.IsBusy = false;
        }
    }

    public async Task PreviewActionAsync(ActionItemViewModel item)
    {
        var preview = await _optimization.PreviewAsync(item.Action.Id);
        item.PreviewText = string.Join(Environment.NewLine, preview.Changes);
    }

    [RelayCommand]
    private async Task UndoAllAsync()
    {
        IsBusy = true;
        try
        {
            var count = await _optimization.UndoAllAsync();
            foreach (var item in Actions) item.IsApplied = item.Action.IsApplied;
            AppliedCount = Actions.Count(a => a.IsApplied);
            StatusMessage = $"{Loc.Instance["opt.undone"]}: {count}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadStartupAsync()
    {
        var entries = await _optimization.GetStartupEntriesAsync();
        StartupEntries.Clear();
        foreach (var entry in entries) StartupEntries.Add(entry);
    }

    [RelayCommand]
    private async Task ToggleStartupAsync(StartupEntry entry)
    {
        await _optimization.SetStartupEntryEnabledAsync(entry, !entry.Enabled);
        await LoadStartupAsync();
    }
}
