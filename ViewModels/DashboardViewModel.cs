using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Monitoring;
using BKKleaner.Optimization;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IOptimizationService _optimization;
    private readonly IRamCleanerService _ramCleaner;
    private readonly ITempCleanerService _tempCleaner;
    private readonly ISystemInfoService _systemInfo;
    private readonly INavigationService _navigation;
    private readonly DispatcherTimer _slowTimer;

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

    // System info
    [ObservableProperty] private string _machineName = "—";
    [ObservableProperty] private string _osDescription = "—";
    [ObservableProperty] private string _cpuName = "—";
    [ObservableProperty] private string _gpuName = "—";
    [ObservableProperty] private string _totalRam = "—";
    [ObservableProperty] private string _uptime = "—";

    // Quick actions
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _quickActionStatus;

    public ObservableCollection<double> CpuHistory { get; } = [];
    public ObservableCollection<double> GpuHistory { get; } = [];
    public ObservableCollection<double> RamHistory { get; } = [];
    public ObservableCollection<ProcessUsage> TopProcesses { get; } = [];

    public DashboardViewModel(
        IHardwareMonitoringService monitoring,
        IOptimizationService optimization,
        IRamCleanerService ramCleaner,
        ITempCleanerService tempCleaner,
        ISystemInfoService systemInfo,
        INavigationService navigation)
    {
        _optimization = optimization;
        _ramCleaner = ramCleaner;
        _tempCleaner = tempCleaner;
        _systemInfo = systemInfo;
        _navigation = navigation;
        monitoring.SnapshotUpdated += OnSnapshot;
        monitoring.WarningRaised += OnWarning;

        LoadSystemInfo();

        // Uptime + top processes refresh on a slower cadence than sensors.
        _slowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _slowTimer.Tick += (_, _) => RefreshSlow();
        _slowTimer.Start();
        RefreshSlow();
    }

    private void LoadSystemInfo()
    {
        var info = _systemInfo.GetSystemInfo();
        MachineName = info.MachineName;
        OsDescription = info.OsDescription;
        CpuName = info.CpuName;
        GpuName = info.GpuName;
        TotalRam = $"{info.TotalRamGb:0.#} GB · {info.LogicalCores} {Loc.Instance["dashboard.cores"]}";
    }

    private void RefreshSlow()
    {
        var up = _systemInfo.GetUptime();
        Uptime = $"{(int)up.TotalDays}d {up.Hours}h {up.Minutes}m";

        var processes = _systemInfo.GetTopProcessesByMemory(6);
        TopProcesses.Clear();
        foreach (var p in processes) TopProcesses.Add(p);
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
            ActiveAlert = $"{Loc.Instance[warning.MetricKey]}: {warning.Value:0.#} (≥ {warning.Threshold:0.#})");
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

    // ---- quick actions --------------------------------------------------------

    [RelayCommand]
    private async Task QuickCleanRamAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        QuickActionStatus = Loc.Instance["ram.cleaning"];
        try
        {
            var result = await _ramCleaner.CleanAsync(true, true, false);
            QuickActionStatus = $"{Loc.Instance["ram.freed"]}: {result.FreedMb:0} MB";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task QuickCleanTempAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        QuickActionStatus = Loc.Instance["temp.scanning"];
        try
        {
            var items = await _tempCleaner.ScanAsync(CleanMode.Smart);
            var result = await _tempCleaner.CleanAsync(items, CleanMode.Smart);
            QuickActionStatus = $"{Loc.Instance["temp.cleaned"]}: {result.ItemsRemoved} · " +
                                $"{result.BytesFreed / 1024.0 / 1024.0:0.#} MB";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void GoToOptimization() => _navigation.NavigateTo("optimization");

    [RelayCommand]
    private void GoToProfiles() => _navigation.NavigateTo("profiles");

    public void Dispose() => _slowTimer.Stop();
}
