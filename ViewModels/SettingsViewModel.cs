using BKKleaner.Localization;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace BKKleaner.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IThemeService _themes;
    private readonly ILocalizationService _localization;

    [ObservableProperty] private string _selectedLanguage;
    [ObservableProperty] private string _selectedTheme;
    [ObservableProperty] private int _monitoringIntervalMs;
    [ObservableProperty] private double _cpuTempWarning;
    [ObservableProperty] private double _gpuTempWarning;
    [ObservableProperty] private double _ramUsageWarning;
    [ObservableProperty] private bool _createRestorePoint;
    [ObservableProperty] private bool _webhookEnabled;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _enableAnimations;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _autoRamCleanEnabled;
    [ObservableProperty] private int _autoRamCleanInterval;
    [ObservableProperty] private string? _validationText;

    public IReadOnlyList<string> Languages => _localization.AvailableLanguages;
    public IReadOnlyList<string> Themes => _themes.AvailableThemes;
    public IReadOnlyList<int> AutoRamIntervals => IAutoRamCleanService.AllowedIntervalsMinutes;

    public SettingsViewModel(ISettingsService settings, IThemeService themes,
        ILocalizationService localization)
    {
        _settings = settings;
        _themes = themes;
        _localization = localization;

        var current = settings.Current;
        _selectedLanguage = current.Language;
        _selectedTheme = current.Theme;
        _monitoringIntervalMs = current.MonitoringIntervalMs;
        _cpuTempWarning = current.CpuTempWarningC;
        _gpuTempWarning = current.GpuTempWarningC;
        _ramUsageWarning = current.RamUsageWarningPercent;
        _createRestorePoint = current.CreateRestorePointBeforeOptimization;
        _webhookEnabled = current.WebhookAutomationEnabled;
        _minimizeToTray = current.MinimizeToTray;
        _closeToTray = current.CloseToTray;
        _enableAnimations = current.EnableAnimations;
        _startWithWindows = current.StartWithWindows;
        _startMinimized = current.StartMinimized;
        _autoRamCleanEnabled = current.AutoRamCleanEnabled;
        _autoRamCleanInterval = AutoRamCleanService.SnapInterval(current.AutoRamCleanIntervalMinutes);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_localization.SetLanguage(value))
            _settings.Update(s => s.Language = value);
    }

    partial void OnSelectedThemeChanged(string value) => _themes.ApplyTheme(value);

    // Live-apply behavior toggles so the effect is immediate.
    partial void OnEnableAnimationsChanged(bool value) =>
        _settings.Update(s => s.EnableAnimations = value);

    partial void OnMinimizeToTrayChanged(bool value) =>
        _settings.Update(s => s.MinimizeToTray = value);

    partial void OnCloseToTrayChanged(bool value) =>
        _settings.Update(s => s.CloseToTray = value);

    partial void OnAutoRamCleanEnabledChanged(bool value) =>
        _settings.Update(s => s.AutoRamCleanEnabled = value);

    partial void OnAutoRamCleanIntervalChanged(int value) =>
        _settings.Update(s => s.AutoRamCleanIntervalMinutes = value);

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settings.Update(s => s.StartWithWindows = value);
        StartupManager.Set(value, StartMinimized);
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settings.Update(s => s.StartMinimized = value);
        if (StartWithWindows) StartupManager.Set(true, value);
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Update(s =>
        {
            s.MonitoringIntervalMs = Math.Clamp(MonitoringIntervalMs, 250, 10000);
            s.CpuTempWarningC = CpuTempWarning;
            s.GpuTempWarningC = GpuTempWarning;
            s.RamUsageWarningPercent = RamUsageWarning;
            s.CreateRestorePointBeforeOptimization = CreateRestorePoint;
            s.WebhookAutomationEnabled = WebhookEnabled;
            s.MinimizeToTray = MinimizeToTray;
            s.CloseToTray = CloseToTray;
            s.EnableAnimations = EnableAnimations;
            s.AutoRamCleanEnabled = AutoRamCleanEnabled;
            s.AutoRamCleanIntervalMinutes = AutoRamCleanInterval;
        });
        ValidationText = Loc.Instance["settings.saved"];
    }

    [RelayCommand]
    private void LoadCustomTheme()
    {
        var dialog = new OpenFileDialog { Filter = "Theme JSON (*.json)|*.json" };
        if (dialog.ShowDialog() == true && _themes.ApplyCustomTheme(dialog.FileName))
            SelectedTheme = "Custom";
    }

    [RelayCommand]
    private void ValidateTranslations()
    {
        var missing = _localization.ValidateTranslations();
        ValidationText = missing.Count == 0
            ? Loc.Instance["settings.translations_ok"]
            : string.Join("; ", missing.Select(kv => $"{kv.Key}: {kv.Value.Count} missing"));
    }
}
