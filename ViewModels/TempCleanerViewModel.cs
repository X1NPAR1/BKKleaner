using System.Collections.ObjectModel;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class TempCleanerViewModel : ObservableObject
{
    private readonly ITempCleanerService _tempCleaner;
    private readonly INotificationService _notifications;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private double _totalSizeMb;
    [ObservableProperty] private CleanMode _selectedMode = CleanMode.Smart;

    public ObservableCollection<TempCleanItem> Items { get; } = [];
    public ObservableCollection<string> Snapshots { get; } = [];
    public IReadOnlyList<CleanMode> Modes { get; } = [CleanMode.Smart, CleanMode.Deep, CleanMode.Preview];

    public TempCleanerViewModel(ITempCleanerService tempCleaner, INotificationService notifications)
    {
        _tempCleaner = tempCleaner;
        _notifications = notifications;
        RefreshSnapshots();
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = Loc.Instance["temp.scanning"];
            var items = await _tempCleaner.ScanAsync(SelectedMode);
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            TotalSizeMb = Math.Round(items.Sum(i => i.SizeBytes) / 1024.0 / 1024.0, 1);
            StatusText = $"{items.Count} {Loc.Instance["temp.items_found"]} · {TotalSizeMb:0.#} MB";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsBusy || Items.Count == 0) return;
        if (SelectedMode == CleanMode.Preview)
        {
            StatusText = Loc.Instance["temp.preview_only"];
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _tempCleaner.CleanAsync(Items.ToList(), SelectedMode);
            StatusText = $"{Loc.Instance["temp.cleaned"]}: {result.ItemsRemoved} · " +
                         $"{Loc.Instance["temp.skipped"]}: {result.ItemsSkipped} · " +
                         $"{result.BytesFreed / 1024.0 / 1024.0:0.#} MB";
            _notifications.Notify(Loc.Instance["temp.title"],
                $"{Loc.Instance["temp.cleaned"]}: {result.ItemsRemoved} · {result.BytesFreed / 1024.0 / 1024.0:0.#} MB",
                NotificationLevel.Success);
            Items.Clear();
            TotalSizeMb = 0;
            RefreshSnapshots();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(string snapshotId)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var restored = await _tempCleaner.RestoreAsync(snapshotId);
            StatusText = $"{Loc.Instance["temp.restored"]}: {restored}";
            RefreshSnapshots();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RefreshSnapshots()
    {
        Snapshots.Clear();
        foreach (var snapshot in _tempCleaner.GetQuarantineSnapshots())
            Snapshots.Add(snapshot);
    }
}
