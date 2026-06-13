using System.IO;
using BKKleaner.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BKKleaner.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private readonly Lock _gate = new();

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
        return new AppSettings();
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
