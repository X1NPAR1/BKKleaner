using System.Globalization;
using System.IO;
using BKKleaner.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace BKKleaner.Services;

public sealed class SettingsService : ISettingsService
{
    private static readonly string[] SupportedLanguages = ["tr", "en", "de", "nl", "ru"];

    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private readonly object _gate = new();

    public AppSettings Current { get; private set; }
    public string DataDirectory { get; }

    public event EventHandler? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger, string? dataDirectory = null)
    {
        _logger = logger;
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BKKleaner");
        Directory.CreateDirectory(DataDirectory);
        _settingsPath = Path.Combine(DataDirectory, "settings.json");
        Current = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var loaded = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(_settingsPath));
                if (loaded is not null) return loaded;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, falling back to defaults");
        }

        // First run: language preference from the installer, then the OS culture.
        return new AppSettings
        {
            Language = ResolveDefaultLanguage(ReadInstallerLanguage(), CultureInfo.CurrentUICulture)
        };
    }

    /// <summary>Picks the initial UI language: installer choice → OS culture → English.</summary>
    internal static string ResolveDefaultLanguage(string? installerLanguage, CultureInfo osCulture)
    {
        if (installerLanguage is not null &&
            SupportedLanguages.Contains(installerLanguage, StringComparer.OrdinalIgnoreCase))
            return installerLanguage.ToLowerInvariant();

        var osLang = osCulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return SupportedLanguages.Contains(osLang) ? osLang : "en";
    }

    private string? ReadInstallerLanguage()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\BKKleaner");
            return key?.GetValue("DefaultLanguage") as string;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Installer language lookup failed");
            return null;
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(Current, Formatting.Indented));
        }
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Save();
    }
}
