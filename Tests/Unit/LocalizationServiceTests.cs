using BKKleaner.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class LocalizationServiceTests
{
    private static LocalizationService Create() =>
        new(NullLogger<LocalizationService>.Instance, TestHelpers.LocalizationDir);

    [Fact]
    public void Loads_all_five_required_languages()
    {
        var svc = Create();
        Assert.Equal(["de", "en", "nl", "ru", "tr"], svc.AvailableLanguages.OrderBy(l => l));
    }

    [Theory]
    [InlineData("tr", "dashboard.title", "Gösterge Paneli")]
    [InlineData("en", "dashboard.title", "Dashboard")]
    [InlineData("de", "nav.settings", "Einstellungen")]
    [InlineData("nl", "nav.settings", "Instellingen")]
    [InlineData("ru", "nav.settings", "Настройки")]
    public void Returns_translation_for_each_language(string lang, string key, string expected)
    {
        var svc = Create();
        Assert.True(svc.SetLanguage(lang));
        Assert.Equal(expected, svc[key]);
    }

    [Fact]
    public void Unknown_language_is_rejected()
    {
        var svc = Create();
        Assert.False(svc.SetLanguage("xx"));
        Assert.Equal("en", svc.CurrentLanguage);
    }

    [Fact]
    public void Missing_key_falls_back_to_key_and_is_recorded()
    {
        var svc = Create();
        var value = svc["this.key.does.not.exist"];
        Assert.Equal("this.key.does.not.exist", value);
        Assert.Contains("this.key.does.not.exist", svc.MissingKeys);
    }

    [Fact]
    public void Language_change_raises_event()
    {
        var svc = Create();
        var raised = false;
        svc.LanguageChanged += (_, _) => raised = true;
        svc.SetLanguage("tr");
        Assert.True(raised);
    }

    [Fact]
    public void All_translations_are_complete_against_english()
    {
        var svc = Create();
        var missing = svc.ValidateTranslations();
        Assert.True(missing.Count == 0,
            "Missing translations: " + string.Join("; ",
                missing.Select(kv => $"{kv.Key}: [{string.Join(", ", kv.Value)}]")));
    }
}
