using System.Collections.ObjectModel;
using System.Windows;
using BKKleaner.Models;
using BKKleaner.Monitoring;
using BKKleaner.Optimization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BKKleaner.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IOptimizationService _optimization;

    [ObservableProperty] private double _cpuTemp;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _gpuTemp;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _ramUsedGb;
    [ObservableProperty] private double _ramTotalGb;
    [ObservableProperty] private double _ramUsagePercent;
    [ObservableProperty] private double _ramMhz;
    [ObservableProperty] private string _diskHealth = "—";
    [ObservableProperty] private double _fpsEstimate;
    [ObservableProperty] private int _healthScore = 100;
    [ObservableProperty] private int _optimizationScore;
    [ObservableProperty] private string? _activeAlert;

    public ObservableCollection<double> CpuHistory { get; } = [];
    public ObservableCollection<double> GpuHistory { get; } = [];
    public ObservableCollection<double> RamHistory { get; } = [];

    public DashboardViewModel(IHardwareMonitoringService monitoring, IOptimizationService optimization)
    {
        _optimization = optimization;
        monitoring.SnapshotUpdated += OnSnapshot;
        monitoring.WarningRaised += OnWarning;
    }

    private void OnSnapshot(object? sender, HardwareSnapshot s)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        dispatcher.BeginInvoke(() =>
        {
            CpuTemp = Math.Round(s.Cpu.TemperatureC, 1);
            CpuUsage = Math.Round(s.Cpu.UsagePercent, 1);
            GpuTemp = Math.Round(s.Gpu.TemperatureC, 1);
            GpuUsage = Math.Round(s.Gpu.UsagePercent, 1);
            RamUsedGb = Math.Round(s.Ram.UsedGb, 2);
            RamTotalGb = Math.Round(s.Ram.TotalGb, 2);
            RamUsagePercent = Math.Round(s.Ram.UsagePercent, 1);
            RamMhz = s.Ram.SpeedMhz;
            DiskHealth = s.Storage.Count == 0 ? "—"
                : s.Storage.Min(d => d.HealthPercent).ToString("0") + "%";

            Push(CpuHistory, CpuUsage);
            Push(GpuHistory, GpuUsage);
            Push(RamHistory, RamUsagePercent);

            FpsEstimate = Math.Round(Benchmark.BenchmarkService.EstimateFps(
                1500, 1.5, s.Cpu.UsagePercent, s.Gpu.UsagePercent), 0);
            HealthScore = ComputeHealthScore(s);
            OptimizationScore = ComputeOptimizationScore();
        });
    }

    private void OnWarning(object? sender, ThresholdWarning warning)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
            ActiveAlert = $"{warning.MetricKey}: {warning.Value:0.#} (≥ {warning.Threshold:0.#})");
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > 120) series.RemoveAt(0);
    }

    private static int ComputeHealthScore(HardwareSnapshot s)
    {
        var score = 100.0;
        if (s.Cpu.TemperatureC > 70) score -= (s.Cpu.TemperatureC - 70) * 1.2;
        if (s.Gpu.TemperatureC > 70) score -= (s.Gpu.TemperatureC - 70) * 1.0;
        if (s.Ram.UsagePercent > 80) score -= (s.Ram.UsagePercent - 80) * 0.8;
        foreach (var disk in s.Storage)
            score -= (100 - disk.HealthPercent) * 0.2;
        return (int)Math.Clamp(score, 0, 100);
    }

    private int ComputeOptimizationScore()
    {
        var total = _optimization.Actions.Count;
        if (total == 0) return 0;
        var applied = _optimization.Actions.Count(a => a.IsApplied);
        return (int)Math.Round(applied / (double)total * 100);
    }
}
