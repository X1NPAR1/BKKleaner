namespace BKKleaner.Models;

/// <summary>A single point-in-time reading of every monitored hardware domain.</summary>
public sealed class HardwareSnapshot
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public CpuMetrics Cpu { get; init; } = new();
    public GpuMetrics Gpu { get; init; } = new();
    public RamMetrics Ram { get; init; } = new();
    public List<StorageMetrics> Storage { get; init; } = [];
    public MainboardMetrics Mainboard { get; init; } = new();
}

public sealed class CpuMetrics
{
    public string Name { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double UsagePercent { get; set; }
    public double FrequencyMhz { get; set; }
    public double Voltage { get; set; }
    public List<double> PerCoreLoad { get; set; } = [];
    public double PackagePowerWatt { get; set; }
}

public sealed class GpuMetrics
{
    public string Name { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double UsagePercent { get; set; }
    public double VramUsedMb { get; set; }
    public double VramTotalMb { get; set; }
    public double FanRpm { get; set; }
    public double PowerWatt { get; set; }
}

public sealed class RamMetrics
{
    public double UsedGb { get; set; }
    public double TotalGb { get; set; }
    public double UsagePercent { get; set; }
    public double SpeedMhz { get; set; }
    public string Timings { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public bool TemperatureSupported { get; set; }
}

public sealed class StorageMetrics
{
    public string Name { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double UsedPercent { get; set; }
    public double TotalGb { get; set; }
    public double ReadMbPerSec { get; set; }
    public double WriteMbPerSec { get; set; }
    public string SmartStatus { get; set; } = "Unknown";
    public double HealthPercent { get; set; } = 100;
}

public sealed class MainboardMetrics
{
    public string Name { get; set; } = string.Empty;
    public List<SensorReading> Sensors { get; set; } = [];
    public List<SensorReading> Fans { get; set; } = [];
}

public sealed record SensorReading(string Name, double Value, string Unit);
