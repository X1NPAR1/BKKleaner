using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BKKleaner.Services;

public sealed class ThemeService : IThemeService
{
    private static readonly string[] BuiltInThemes = ["Light", "Dark", "Gaming", "RgbNeon", "Minimal"];

    private readonly ILogger<ThemeService> _logger;
    private readonly ISettingsService _settings;
    private ResourceDictionary? _activeThemeDictionary;

    public IReadOnlyList<string> AvailableThemes => BuiltInThemes;
    public string CurrentTheme { get; private set; } = "Dark";

    public event EventHandler? ThemeChanged;

    public ThemeService(ILogger<ThemeService> logger, ISettingsService settings)
    {
        _logger = logger;
        _settings = settings;
    }

    public bool ApplyTheme(string themeName)
    {
        var match = BuiltInThemes.FirstOrDefault(
            t => string.Equals(t, themeName, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            _logger.LogWarning("Unknown theme requested: {Theme}", themeName);
            return false;
        }

        // Outside a running Application (unit tests) only the state is switched.
        if (Application.Current is not null)
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/Themes/{match}.xaml", UriKind.Absolute)
            };
            Swap(dict);
        }
        CurrentTheme = match;
        _settings.Update(s => s.Theme = match);
        _logger.LogInformation("Theme switched to {Theme}", match);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool ApplyCustomTheme(string jsonPath)
    {
        try
        {
            var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(jsonPath));
            if (map is null || map.Count == 0) return false;

            // Start from Dark as the base so missing keys keep sensible values.
            var dict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute)
            };
            foreach (var (key, hex) in map)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                dict[key] = brush;
            }

            Swap(dict);
            CurrentTheme = "Custom";
            _settings.Update(s => { s.Theme = "Custom"; s.CustomThemePath = jsonPath; });
            _logger.LogInformation("Custom theme applied from {Path}", jsonPath);
            ThemeChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply custom theme from {Path}", jsonPath);
            return false;
        }
    }

    private void Swap(ResourceDictionary newTheme)
    {
        var app = Application.Current;
        if (app is null) return;

        if (_activeThemeDictionary is not null)
            app.Resources.MergedDictionaries.Remove(_activeThemeDictionary);
        app.Resources.MergedDictionaries.Add(newTheme);
        _activeThemeDictionary = newTheme;
    }
}
