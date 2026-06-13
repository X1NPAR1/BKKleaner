using System.Windows;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class RamCleanerViewModel : ObservableObject
{
    private readonly IRamCleanerService _ramCleaner;
    private readonly ISettingsService _settings;
    private readonly IAutoRamCleanService _autoClean;

    [ObservableProperty] private bool _trimWorkingSets = true;
    [ObservableProperty] private bool _clearStandbyList = true;
    [ObservableProperty] private bool _optimizeCache;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _resultText;
    [ObservableProperty] private double _usageBefore;
    [ObservableProperty] private double _usageAfter;
    [ObservableProperty] private double _freedMb;
    [ObservableProperty] private int _processesTrimmed;

    // Automatic cleaning
    [ObservableProperty] private bool _autoEnabled;
    [ObservableProperty] private int _autoInterval;
    [ObservableProperty] private string? _autoStatus;

    public IReadOnlyList<int> AutoIntervals => IAutoRamCleanService.AllowedIntervalsMinutes;

    public RamCleanerViewModel(IRamCleanerService ramCleaner, ISettingsService settings,
        IAutoRamCleanService autoClean)
    {
        _ramCleaner = ramCleaner;
        _settings = settings;
        _autoClean = autoClean;

        _autoEnabled = settings.Current.AutoRamCleanEnabled;
        _autoInterval = AutoRamCleanService.SnapInterval(settings.Current.AutoRamCleanIntervalMinutes);

        _autoClean.AutoCleanCompleted += (_, result) =>
            Application.Current?.Dispatcher.BeginInvoke(() =>
                AutoStatus = $"{Loc.Instance["ram.auto_last"]}: {DateTime.Now:HH:mm} · {result.FreedMb:0} MB");
    }

    partial void OnAutoEnabledChanged(bool value) =>
        _settings.Update(s => s.AutoRamCleanEnabled = value);

    partial void OnAutoIntervalChanged(int value) =>
        _settings.Update(s => s.AutoRamCleanIntervalMinutes = value);

    [RelayCommand]
    private async Task CleanAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _ramCleaner.CleanAsync(TrimWorkingSets, ClearStandbyList, OptimizeCache);
            UsageBefore = result.UsageBeforePercent;
            UsageAfter = result.UsageAfterPercent;
            FreedMb = Math.Round(result.FreedMb, 0);
            ProcessesTrimmed = result.ProcessesTrimmed;
            ResultText = $"{Loc.Instance["ram.freed"]}: {FreedMb:0} MB · " +
                         $"{Loc.Instance["ram.trimmed"]}: {result.ProcessesTrimmed} · " +
                         $"{result.UsageBeforePercent:0}% → {result.UsageAfterPercent:0}%";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
