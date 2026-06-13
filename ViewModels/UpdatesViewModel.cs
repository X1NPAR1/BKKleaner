using System.Collections.ObjectModel;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class UpdateItemViewModel : ObservableObject
{
    public required UpdateItem Item { get; init; }
    [ObservableProperty] private bool _isSelected = true;

    public string Name => Item.Name;
    public bool IsInstallable => Item.IsInstallable;

    public string Detail => Item.InfoKey is not null
        ? Loc.Instance[Item.InfoKey]
        : $"{Item.CurrentVersion} → {Item.AvailableVersion}";
}

public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly IUpdateService _updates;
    private readonly INotificationService _notifications;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string? _selfUpdateText;
    [ObservableProperty] private bool _wingetAvailable = true;
    [ObservableProperty] private bool _hasChecked;

    public ObservableCollection<UpdateItemViewModel> Items { get; } = [];

    public int InstallableCount => Items.Count(i => i.IsInstallable);

    public UpdatesViewModel(IUpdateService updates, INotificationService notifications)
    {
        _updates = updates;
        _notifications = notifications;
    }

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await RunCheckAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunCheckAsync()
    {
        WingetAvailable = _updates.IsWingetAvailable;
        StatusText = Loc.Instance["updates.checking"];
        var found = await _updates.CheckForUpdatesAsync();
        Items.Clear();
        foreach (var item in found)
            Items.Add(new UpdateItemViewModel { Item = item });
        HasChecked = true;
        OnPropertyChanged(nameof(InstallableCount));
        StatusText = $"{InstallableCount} {Loc.Instance["updates.found"]}";

        var newVersion = await _updates.CheckSelfUpdateAsync();
        SelfUpdateText = newVersion is null
            ? Loc.Instance["updates.app_current"]
            : $"{Loc.Instance["updates.app_available"]}: {newVersion}";
    }

    [RelayCommand]
    private async Task UpgradeAsync(UpdateItemViewModel item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = $"{Loc.Instance["updates.installing"]}: {item.Name}";
            var ok = await _updates.UpgradeAsync(item.Item);
            StatusText = ok
                ? $"{item.Name} ✓"
                : $"{item.Name} — {Loc.Instance["updates.failed"]}";
            if (ok && item.IsInstallable) Items.Remove(item);
            OnPropertyChanged(nameof(InstallableCount));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task UpgradeSelectedAsync() =>
        UpgradeManyAsync(Items.Where(i => i.IsInstallable && i.IsSelected).ToList());

    [RelayCommand]
    private Task UpgradeAllAsync() =>
        UpgradeManyAsync(Items.Where(i => i.IsInstallable).ToList());

    private async Task UpgradeManyAsync(List<UpdateItemViewModel> targets)
    {
        if (IsBusy || targets.Count == 0) return;
        IsBusy = true;
        try
        {
            var progress = new Progress<string>(name =>
                StatusText = $"{Loc.Instance["updates.installing"]}: {name}");
            var ok = await _updates.UpgradeAllAsync(targets.Select(t => t.Item), progress);
            _notifications.Notify(Loc.Instance["updates.title"],
                $"{Loc.Instance["updates.completed"]}: {ok}/{targets.Count}", NotificationLevel.Success);

            // Refresh the list so applied updates drop off (IsBusy already held here).
            await RunCheckAsync();
            StatusText = $"{Loc.Instance["updates.completed"]}: {ok}/{targets.Count}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenWindowsUpdate() => _updates.OpenWindowsUpdate();
}
