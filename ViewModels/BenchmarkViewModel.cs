using BKKleaner.Benchmark;
using BKKleaner.Localization;
using BKKleaner.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class BenchmarkViewModel : ObservableObject
{
    private readonly IBenchmarkService _benchmark;
    private BenchmarkResult? _before;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private BenchmarkResult? _lastResult;
    [ObservableProperty] private BenchmarkResult? _beforeResult;
    [ObservableProperty] private string? _comparisonText;
    [ObservableProperty] private string? _reportPath;

    public BenchmarkViewModel(IBenchmarkService benchmark) => _benchmark = benchmark;

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            StatusText = Loc.Instance["bench.running"];
            LastResult = await _benchmark.RunAsync("manual");
            StatusText = Loc.Instance["bench.done"];

            if (_before is not null)
            {
                var comparison = _benchmark.Compare(_before, LastResult);
                ReportPath = await _benchmark.ExportReportAsync(comparison);
                ComparisonText =
                    $"FPS {comparison.FpsDeltaPercent:+0.#;-0.#;0}% · " +
                    $"CPU {comparison.CpuLoadDelta:+0.#;-0.#;0} · " +
                    $"RAM {comparison.RamDelta:+0.#;-0.#;0} · " +
                    $"Latency {comparison.LatencyDeltaPercent:+0.#;-0.#;0}%";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void MarkAsBaseline()
    {
        if (LastResult is null) return;
        _before = LastResult;
        BeforeResult = LastResult;
        ComparisonText = null;
        StatusText = Loc.Instance["bench.baseline_set"];
    }
}
