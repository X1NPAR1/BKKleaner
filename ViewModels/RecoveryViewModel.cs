using System.Collections.ObjectModel;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Recovery;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class RecoveryViewModel : ObservableObject
{
    private readonly IRecoveryService _recovery;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;

    public ObservableCollection<RecoveryPoint> Points { get; } = [];

    public RecoveryViewModel(IRecoveryService recovery)
    {
        _recovery = recovery;
        Refresh();
    }

    private void Refresh()
    {
        Points.Clear();
        foreach (var point in _recovery.GetRecoveryPoints()) Points.Add(point);
    }

    [RelayCommand]
    private async Task CreateFullBackupAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = Loc.Instance["recovery.creating"];
            var created = await _recovery.CreateFullBackupAsync("manual");
            StatusText = $"{Loc.Instance["recovery.created"]}: {created.Count}";
            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(RecoveryPoint point)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var ok = await _recovery.RestoreAsync(point);
            StatusText = ok ? Loc.Instance["recovery.restored"] : Loc.Instance["recovery.restore_failed"];
        }
        finally
        {
            IsBusy = false;
        }
    }
}
