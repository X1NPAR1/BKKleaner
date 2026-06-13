namespace BKKleaner.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "Dark";
    public int MonitoringIntervalMs { get; set; } = 1000;
    public double CpuTempWarningC { get; set; } = 85;
    public double GpuTempWarningC { get; set; } = 85;
    public double RamUsageWarningPercent { get; set; } = 90;
    public bool CreateRestorePointBeforeOptimization { get; set; } = true;
    public bool WebhookAutomationEnabled { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public string? CustomThemePath { get; set; }
}
