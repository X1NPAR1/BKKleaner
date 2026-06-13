using System.Collections.ObjectModel;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class UpdatesViewModel : ObservableObject
{
    private readonly IUpdateService _updates;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string? _selfUpdateText;

    public ObservableCollection<UpdateItem> Items { get; } = [];

    public UpdatesViewModel(IUpdateService updates) => _updates = updates;

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = Loc.Instance["updates.checking"];
            var items = await _updates.CheckForUpdatesAsync();
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            StatusText = $"{items.Count} {Loc.Instance["updates.found"]}";

            var newVersion = await _updates.CheckSelfUpdateAsync();
            SelfUpdateText = newVersion is null
                ? Loc.Instance["updates.app_current"]
                : $"{Loc.Instance["updates.app_available"]}: {newVersion}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpgradeAsync(UpdateItem item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = $"{Loc.Instance["updates.installing"]}: {item.Name}";
            var ok = await _updates.UpgradeAsync(item);
            StatusText = ok
                ? $"{item.Name} ✓"
                : $"{item.Name} — {Loc.Instance["updates.failed"]}";
            if (ok) Items.Remove(item);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenWindowsUpdate() => _updates.OpenWindowsUpdate();
}
