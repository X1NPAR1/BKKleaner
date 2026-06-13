using BKKleaner.Localization;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class RamCleanerViewModel : ObservableObject
{
    private readonly IRamCleanerService _ramCleaner;

    [ObservableProperty] private bool _trimWorkingSets = true;
    [ObservableProperty] private bool _clearStandbyList = true;
    [ObservableProperty] private bool _optimizeCache;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _resultText;
    [ObservableProperty] private double _usageBefore;
    [ObservableProperty] private double _usageAfter;
    [ObservableProperty] private double _freedMb;
    [ObservableProperty] private int _processesTrimmed;

    public RamCleanerViewModel(IRamCleanerService ramCleaner) => _ramCleaner = ramCleaner;

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
