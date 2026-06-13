using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BKKleaner.Models;
using BKKleaner.Monitoring;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class MonitoringViewModel : ObservableObject
{
    private readonly IHardwareMonitoringService _monitoring;

    [ObservableProperty] private string _cpuName = "—";
    [ObservableProperty] private double _cpuTemp;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _cpuMhz;
    [ObservableProperty] private double _cpuVoltage;
    [ObservableProperty] private double _cpuWatt;
    [ObservableProperty] private string _gpuName = "—";
    [ObservableProperty] private double _gpuTemp;
    [ObservableProperty] private double _gpuUsage;
    [ObservableProperty] private double _vramUsedMb;
    [ObservableProperty] private double _vramTotalMb;
    [ObservableProperty] private double _gpuFanRpm;
    [ObservableProperty] private double _gpuWatt;
    [ObservableProperty] private double _ramUsedGb;
    [ObservableProperty] private double _ramTotalGb;
    [ObservableProperty] private double _ramMhz;
    [ObservableProperty] private string _ramTimings = "N/A";
    [ObservableProperty] private string _ramTemp = "N/A";
    [ObservableProperty] private string _mainboardName = "—";
    [ObservableProperty] private string? _exportStatus;

    public ObservableCollection<double> PerCoreLoad { get; } = [];
    public ObservableCollection<StorageMetrics> Disks { get; } = [];
    public ObservableCollection<SensorReading> Fans { get; } = [];
    public ObservableCollection<SensorReading> BoardSensors { get; } = [];
    public ObservableCollection<double> CpuTempHistory { get; } = [];
    public ObservableCollection<double> GpuTempHistory { get; } = [];

    public MonitoringViewModel(IHardwareMonitoringService monitoring)
    {
        _monitoring = monitoring;
        monitoring.SnapshotUpdated += OnSnapshot;
    }

    private void OnSnapshot(object? sender, HardwareSnapshot s)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            CpuName = s.Cpu.Name;
            CpuTemp = Math.Round(s.Cpu.TemperatureC, 1);
            CpuUsage = Math.Round(s.Cpu.UsagePercent, 1);
            CpuMhz = Math.Round(s.Cpu.FrequencyMhz, 0);
            CpuVoltage = Math.Round(s.Cpu.Voltage, 3);
            CpuWatt = Math.Round(s.Cpu.PackagePowerWatt, 1);
            GpuName = string.IsNullOrEmpty(s.Gpu.Name) ? "—" : s.Gpu.Name;
            GpuTemp = Math.Round(s.Gpu.TemperatureC, 1);
            GpuUsage = Math.Round(s.Gpu.UsagePercent, 1);
            VramUsedMb = Math.Round(s.Gpu.VramUsedMb, 0);
            VramTotalMb = Math.Round(s.Gpu.VramTotalMb, 0);
            GpuFanRpm = Math.Round(s.Gpu.FanRpm, 0);
            GpuWatt = Math.Round(s.Gpu.PowerWatt, 1);
            RamUsedGb = Math.Round(s.Ram.UsedGb, 2);
            RamTotalGb = Math.Round(s.Ram.TotalGb, 2);
            RamMhz = s.Ram.SpeedMhz;
            RamTimings = s.Ram.Timings;
            RamTemp = s.Ram.TemperatureSupported ? $"{s.Ram.TemperatureC:0.#} °C" : "N/A";
            MainboardName = string.IsNullOrEmpty(s.Mainboard.Name) ? "—" : s.Mainboard.Name;

            PerCoreLoad.Clear();
            foreach (var load in s.Cpu.PerCoreLoad) PerCoreLoad.Add(Math.Round(load, 0));

            Disks.Clear();
            foreach (var disk in s.Storage) Disks.Add(disk);

            Fans.Clear();
            foreach (var fan in s.Mainboard.Fans) Fans.Add(fan);
            BoardSensors.Clear();
            foreach (var sensor in s.Mainboard.Sensors.Take(12)) BoardSensors.Add(sensor);

            Push(CpuTempHistory, s.Cpu.TemperatureC);
            Push(GpuTempHistory, s.Gpu.TemperatureC);
        });
    }

    private static void Push(ObservableCollection<double> series, double value)
    {
        series.Add(value);
        while (series.Count > 120) series.RemoveAt(0);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"BKKleaner_monitoring_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        await _monitoring.ExportHistoryAsync(path);
        ExportStatus = path;
    }
}
