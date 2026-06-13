namespace BKKleaner.Localization;

public interface ILocalizationService
{
    /// <summary>Currently active language code (tr, en, de, nl, ru).</summary>
    string CurrentLanguage { get; }

    IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>Keys requested at runtime that were missing in every dictionary.</summary>
    IReadOnlyCollection<string> MissingKeys { get; }

    event EventHandler? LanguageChanged;

    /// <summary>Translated value with automatic fallback to English, then to the key itself.</summary>
    string this[string key] { get; }

    bool SetLanguage(string languageCode);

    /// <summary>Compares every dictionary against English and returns keys missing per language.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> ValidateTranslations();
}
