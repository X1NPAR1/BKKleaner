using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class LogsViewModel : ObservableObject
{
    private readonly ILogStore _logStore;

    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string _filterLevel = "All";

    public ICollectionView LogView { get; }
    public IReadOnlyList<string> Levels { get; } = ["All", "Information", "Warning", "Error"];

    public LogsViewModel(ILogStore logStore)
    {
        _logStore = logStore;
        BindingOperations.EnableCollectionSynchronization(logStore.Entries, logStore.SyncRoot);
        LogView = CollectionViewSource.GetDefaultView(logStore.Entries);
        LogView.Filter = Filter;
        LogView.SortDescriptions.Add(new SortDescription(nameof(LogEntry.Timestamp), ListSortDirection.Descending));
    }

    private bool Filter(object obj) =>
        obj is LogEntry entry &&
        (FilterLevel == "All" || string.Equals(entry.Level, FilterLevel, StringComparison.OrdinalIgnoreCase));

    partial void OnFilterLevelChanged(string value) => LogView.Refresh();

    [RelayCommand]
    private void Export()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"BKKleaner_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        _logStore.Export(path);
        StatusText = $"{Loc.Instance["logs.exported"]}: {path}";
    }

    [RelayCommand]
    private void Clear()
    {
        _logStore.Clear();
        StatusText = Loc.Instance["logs.cleared"];
    }
}
