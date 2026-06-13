using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Recovery;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

/// <summary>A recovery point wrapped for display: grouped by its backup session date-time.</summary>
public sealed class RecoveryPointViewModel(RecoveryPoint point)
{
    public RecoveryPoint Point { get; } = point;

    /// <summary>Groups all parts of one backup run together; distinct runs (even same day) differ by time.</summary>
    public string SessionKey => Point.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
    public DateTime CreatedAt => Point.CreatedAt;
    public string Time => Point.CreatedAt.ToString("HH:mm:ss");
    public string Description => Point.Description;
    public string KindLabel => Loc.Instance[$"recovery.kind.{Point.Kind}".ToLowerInvariant()];
}

public sealed partial class RecoveryViewModel : ObservableObject
{
    private readonly IRecoveryService _recovery;
    private readonly Services.INotificationService _notifications;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;

    public ObservableCollection<RecoveryPointViewModel> Points { get; } = [];

    /// <summary>Points grouped by backup session (date-time), newest first.</summary>
    public ICollectionView GroupedPoints { get; }

    public RecoveryViewModel(IRecoveryService recovery, Services.INotificationService notifications)
    {
        _recovery = recovery;
        _notifications = notifications;

        GroupedPoints = CollectionViewSource.GetDefaultView(Points);
        GroupedPoints.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RecoveryPointViewModel.SessionKey)));
        GroupedPoints.SortDescriptions.Add(
            new SortDescription(nameof(RecoveryPointViewModel.CreatedAt), ListSortDirection.Descending));

        Refresh();
    }

    private void Refresh()
    {
        Points.Clear();
        foreach (var point in _recovery.GetRecoveryPoints())
            Points.Add(new RecoveryPointViewModel(point));
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
            _notifications.Notify(Loc.Instance["recovery.title"], StatusText, Services.NotificationLevel.Success);
            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(RecoveryPointViewModel item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var ok = await _recovery.RestoreAsync(item.Point);
            StatusText = ok ? Loc.Instance["recovery.restored"] : Loc.Instance["recovery.restore_failed"];
        }
        finally
        {
            IsBusy = false;
        }
    }
}
