using System.Globalization;
using System.IO;
using System.Management;
using System.Text;
using BKKleaner.Models;
using BKKleaner.Services;
using LibreHardwareMonitor.Hardware;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Monitoring;

public sealed class HardwareMonitoringService : IHardwareMonitoringService
{
    private const int MaxHistory = 600;

    private readonly ILogger<HardwareMonitoringService> _logger;
    private readonly ISettingsService _settings;
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private readonly List<HardwareSnapshot> _history = [];
    private readonly object _historyGate = new();

    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private double _ramSpeedMhz;
    private string _ramTimings = "N/A";
    private bool _wmiQueried;

    public HardwareSnapshot? Latest { get; private set; }

    public IReadOnlyList<HardwareSnapshot> History
    {
        get { lock (_historyGate) return _history.ToList(); }
    }

    public event EventHandler<HardwareSnapshot>? SnapshotUpdated;
    public event EventHandler<ThresholdWarning>? WarningRaised;

    public HardwareMonitoringService(ILogger<HardwareMonitoringService> logger, ISettingsService settings)
    {
        _logger = logger;
        _settings = settings;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsMotherboardEnabled = true
        };
    }

    public void Start()
    {
        if (_pollTask is not null) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _pollTask = Task.Run(async () =>
        {
            try
            {
                _computer.Open();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open hardware monitor; sensor data will be limited");
            }

            QueryRamInfoOnce();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var snapshot = Poll();
                    Latest = snapshot;
                    lock (_historyGate)
                    {
                        _history.Add(snapshot);
                        while (_history.Count > MaxHistory) _history.RemoveAt(0);
                    }
                    SnapshotUpdated?.Invoke(this, snapshot);
                    CheckThresholds(snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Sensor poll failed");
                }

                try
                {
                    await Task.Delay(Math.Max(250, _settings.Current.MonitoringIntervalMs), token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
        _logger.LogInformation("Hardware monitoring started");
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _pollTask?.Wait(TimeSpan.FromSeconds(3)); } catch { /* shutdown */ }
        _pollTask = null;
        _cts?.Dispose();
        _cts = null;
        _logger.LogInformation("Hardware monitoring stopped");
    }

    private void QueryRamInfoOnce()
    {
        if (_wmiQueried) return;
        _wmiQueried = true;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");
            foreach (var obj in searcher.Get())
            {
                var configured = Convert.ToDouble(obj["ConfiguredClockSpeed"] ?? 0d);
                var rated = Convert.ToDouble(obj["Speed"] ?? 0d);
                _ramSpeedMhz = Math.Max(_ramSpeedMhz, configured > 0 ? configured : rated);
            }
            if (_ramSpeedMhz > 0)
                _ramTimings = $"{_ramSpeedMhz:0} MHz (configured)";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not query RAM speed via WMI");
        }
    }

    private HardwareSnapshot Poll()
    {
        _computer.Accept(_visitor);

        var snapshot = new HardwareSnapshot();

        foreach (var hw in _computer.Hardware)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    ReadCpu(hw, snapshot.Cpu);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    // Prefer the discrete GPU when several adapters exist.
                    if (string.IsNullOrEmpty(snapshot.Gpu.Name) || hw.HardwareType != HardwareType.GpuIntel)
                        ReadGpu(hw, snapshot.Gpu);
                    break;
                case HardwareType.Memory:
                    ReadMemory(hw, snapshot.Ram);
                    break;
                case HardwareType.Storage:
                    snapshot.Storage.Add(ReadStorage(hw));
                    break;
                case HardwareType.Motherboard:
                    ReadMotherboard(hw, snapshot.Mainboard);
                    break;
            }
        }

        snapshot.Ram.SpeedMhz = _ramSpeedMhz;
        snapshot.Ram.Timings = _ramTimings;
        if (snapshot.Ram.TotalGb > 0)
            snapshot.Ram.UsagePercent = snapshot.Ram.UsedGb / snapshot.Ram.TotalGb * 100.0;

        return snapshot;
    }

    /// <summary>Reads a sensor value, normalizing null/NaN/Infinity to 0 for display safety.</summary>
    private static double SafeValue(ISensor s)
    {
        var v = s.Value.GetValueOrDefault();
        return double.IsNaN(v) || double.IsInfinity(v) ? 0 : v;
    }

    private static void ReadCpu(IHardware hw, CpuMetrics cpu)
    {
        cpu.Name = hw.Name;
        foreach (var s in hw.Sensors)
        {
            var v = SafeValue(s);
            switch (s.SensorType)
            {
                case SensorType.Temperature when s.Name.Contains("Package") || s.Name.Contains("Tctl") || s.Name.Contains("Average"):
                    if (v > 0) cpu.TemperatureC = v;
                    break;
                case SensorType.Temperature when cpu.TemperatureC <= 0 && v > 0:
                    cpu.TemperatureC = v;
                    break;
                case SensorType.Load when s.Name == "CPU Total":
                    cpu.UsagePercent = v;
                    break;
                case SensorType.Load when s.Name.StartsWith("CPU Core #", StringComparison.Ordinal):
                    cpu.PerCoreLoad.Add(v);
                    break;
                case SensorType.Clock when s.Name.Contains("Core"):
                    cpu.FrequencyMhz = Math.Max(cpu.FrequencyMhz, v);
                    break;
                case SensorType.Voltage when s.Name.Contains("Core"):
                    if (cpu.Voltage <= 0) cpu.Voltage = v;
                    break;
                case SensorType.Power when s.Name.Contains("Package"):
                    cpu.PackagePowerWatt = v;
                    break;
            }
        }
    }

    private static void ReadGpu(IHardware hw, GpuMetrics gpu)
    {
        gpu.Name = hw.Name;
        foreach (var s in hw.Sensors)
        {
            var v = SafeValue(s);
            switch (s.SensorType)
            {
                case SensorType.Temperature when s.Name.Contains("Core"):
                    gpu.TemperatureC = v;
                    break;
                case SensorType.Load when s.Name.Contains("Core"):
                    gpu.UsagePercent = v;
                    break;
                case SensorType.SmallData when s.Name.Contains("Memory Used"):
                    gpu.VramUsedMb = v;
                    break;
                case SensorType.SmallData when s.Name.Contains("Memory Total"):
                    gpu.VramTotalMb = v;
                    break;
                case SensorType.Fan:
                    gpu.FanRpm = Math.Max(gpu.FanRpm, v);
                    break;
                case SensorType.Power:
                    if (gpu.PowerWatt <= 0) gpu.PowerWatt = v;
                    break;
            }
        }
    }

    private static void ReadMemory(IHardware hw, RamMetrics ram)
    {
        double used = 0, available = 0;
        foreach (var s in hw.Sensors)
        {
            var v = SafeValue(s);
            switch (s.SensorType)
            {
                case SensorType.Data when s.Name == "Memory Used":
                    used = v;
                    break;
                case SensorType.Data when s.Name == "Memory Available":
                    available = v;
                    break;
                case SensorType.Temperature:
                    ram.TemperatureC = v;
                    ram.TemperatureSupported = true;
                    break;
            }
        }
        ram.UsedGb = used;
        ram.TotalGb = used + available;
    }

    private static StorageMetrics ReadStorage(IHardware hw)
    {
        var storage = new StorageMetrics { Name = hw.Name };
        foreach (var s in hw.Sensors)
        {
            var v = SafeValue(s);
            switch (s.SensorType)
            {
                case SensorType.Temperature:
                    storage.TemperatureC = v;
                    break;
                case SensorType.Load when s.Name.Contains("Used"):
                    storage.UsedPercent = v;
                    break;
                case SensorType.Throughput when s.Name.Contains("Read"):
                    storage.ReadMbPerSec = v / 1024.0 / 1024.0;
                    break;
                case SensorType.Throughput when s.Name.Contains("Write"):
                    storage.WriteMbPerSec = v / 1024.0 / 1024.0;
                    break;
                case SensorType.Level when s.Name.Contains("Life"):
                    storage.HealthPercent = v;
                    break;
            }
        }
        storage.SmartStatus = storage.HealthPercent >= 80 ? "OK"
            : storage.HealthPercent >= 50 ? "Warning" : "Critical";
        return storage;
    }

    private static void ReadMotherboard(IHardware hw, MainboardMetrics board)
    {
        board.Name = hw.Name;
        foreach (var sub in hw.SubHardware)
        {
            foreach (var s in sub.Sensors)
            {
                var v = SafeValue(s);
                switch (s.SensorType)
                {
                    case SensorType.Fan:
                        board.Fans.Add(new SensorReading(s.Name, v, "RPM"));
                        break;
                    case SensorType.Temperature:
                        board.Sensors.Add(new SensorReading(s.Name, v, "Â°C"));
                        break;
                    case SensorType.Voltage:
                        board.Sensors.Add(new SensorReading(s.Name, v, "V"));
                        break;
                }
            }
        }
    }

    private void CheckThresholds(HardwareSnapshot s)
    {
        var cfg = _settings.Current;
        if (s.Cpu.TemperatureC >= cfg.CpuTempWarningC)
            WarningRaised?.Invoke(this, new ThresholdWarning("warning.cpu_temp", s.Cpu.TemperatureC, cfg.CpuTempWarningC));
        if (s.Gpu.TemperatureC >= cfg.GpuTempWarningC)
            WarningRaised?.Invoke(this, new ThresholdWarning("warning.gpu_temp", s.Gpu.TemperatureC, cfg.GpuTempWarningC));
        if (s.Ram.UsagePercent >= cfg.RamUsageWarningPercent)
            WarningRaised?.Invoke(this, new ThresholdWarning("warning.ram_usage", s.Ram.UsagePercent, cfg.RamUsageWarningPercent));
    }

    public async Task ExportHistoryAsync(string filePath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("timestamp,cpu_temp_c,cpu_usage,cpu_mhz,gpu_temp_c,gpu_usage,vram_used_mb,ram_used_gb,ram_total_gb,ram_usage");
        foreach (var s in History)
        {
            ct.ThrowIfCancellationRequested();
            sb.AppendLine(string.Join(',',
                s.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                s.Cpu.TemperatureC.ToString("0.0", CultureInfo.InvariantCulture),
                s.Cpu.UsagePercent.ToString("0.0", CultureInfo.InvariantCulture),
                s.Cpu.FrequencyMhz.ToString("0", CultureInfo.InvariantCulture),
                s.Gpu.TemperatureC.ToString("0.0", CultureInfo.InvariantCulture),
                s.Gpu.UsagePercent.ToString("0.0", CultureInfo.InvariantCulture),
                s.Gpu.VramUsedMb.ToString("0", CultureInfo.InvariantCulture),
                s.Ram.UsedGb.ToString("0.00", CultureInfo.InvariantCulture),
                s.Ram.TotalGb.ToString("0.00", CultureInfo.InvariantCulture),
                s.Ram.UsagePercent.ToString("0.0", CultureInfo.InvariantCulture)));
        }
        await File.WriteAllTextAsync(filePath, sb.ToString(), ct).ConfigureAwait(false);
        _logger.LogInformation("Monitoring history exported to {Path}", filePath);
    }

    public void Dispose()
    {
        Stop();
        try { _computer.Close(); } catch { /* native cleanup */ }
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware) sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
