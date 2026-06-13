using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BKKleaner.Localization;

public sealed class LocalizationService : ILocalizationService
{
    private const string FallbackLanguage = "en";

    private readonly ILogger<LocalizationService> _logger;
    private readonly Dictionary<string, Dictionary<string, string>> _dictionaries = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _missingKeys = new();

    public string CurrentLanguage { get; private set; } = FallbackLanguage;
    public IReadOnlyList<string> AvailableLanguages { get; }
    public IReadOnlyCollection<string> MissingKeys => _missingKeys.Keys.ToList();

    public event EventHandler? LanguageChanged;

    public LocalizationService(ILogger<LocalizationService> logger, string? localizationDirectory = null)
    {
        _logger = logger;
        var dir = localizationDirectory
                  ?? Path.Combine(AppContext.BaseDirectory, "Localization");

        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                var code = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                try
                {
                    var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(file));
                    if (map is not null) _dictionaries[code] = map;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load localization file {File}", file);
                }
            }
        }

        if (_dictionaries.Count == 0)
        {
            _logger.LogError("No localization dictionaries found in {Dir}", dir);
            _dictionaries[FallbackLanguage] = [];
        }

        AvailableLanguages = _dictionaries.Keys.OrderBy(k => k).ToList();
    }

    public string this[string key]
    {
        get
        {
            if (_dictionaries.TryGetValue(CurrentLanguage, out var dict) &&
                dict.TryGetValue(key, out var value))
                return value;

            // Automatic fallback to English.
            if (_dictionaries.TryGetValue(FallbackLanguage, out var fallback) &&
                fallback.TryGetValue(key, out var fbValue))
                return fbValue;

            // Missing key detection.
            if (_missingKeys.TryAdd(key, 0))
                _logger.LogWarning("Missing localization key: {Key}", key);
            return key;
        }
    }

    public bool SetLanguage(string languageCode)
    {
        if (!_dictionaries.ContainsKey(languageCode)) return false;
        if (string.Equals(CurrentLanguage, languageCode, StringComparison.OrdinalIgnoreCase)) return true;

        CurrentLanguage = languageCode.ToLowerInvariant();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateTranslations()
    {
        var result = new Dictionary<string, IReadOnlyList<string>>();
        if (!_dictionaries.TryGetValue(FallbackLanguage, out var reference)) return result;

        foreach (var (code, dict) in _dictionaries)
        {
            if (code == FallbackLanguage) continue;
            var missing = reference.Keys.Where(k => !dict.ContainsKey(k)).ToList();
            if (missing.Count > 0) result[code] = missing;
        }
        return result;
    }
}
