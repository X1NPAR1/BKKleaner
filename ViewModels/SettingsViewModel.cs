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
    [ObservableProperty] private string? _validationText;

    public IReadOnlyList<string> Languages => _localization.AvailableLanguages;
    public IReadOnlyList<string> Themes => _themes.AvailableThemes;

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
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_localization.SetLanguage(value))
            _settings.Update(s => s.Language = value);
    }

    partial void OnSelectedThemeChanged(string value) => _themes.ApplyTheme(value);

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
