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
    public string? CustomThemePath { get; set; }

    // Behavior
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool EnableAnimations { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }

    // Automatic RAM cleaning
    public bool AutoRamCleanEnabled { get; set; }
    public int AutoRamCleanIntervalMinutes { get; set; } = 30;
}
