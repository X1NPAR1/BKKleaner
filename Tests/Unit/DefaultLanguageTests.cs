using System.Globalization;
using BKKleaner.Services;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class DefaultLanguageTests
{
    [Theory]
    [InlineData("tr", "en-US", "tr")]
    [InlineData("DE", "en-US", "de")]
    [InlineData("ru", "tr-TR", "ru")]
    public void Installer_choice_wins_when_supported(string installer, string os, string expected)
    {
        Assert.Equal(expected, SettingsService.ResolveDefaultLanguage(installer, new CultureInfo(os)));
    }

    [Theory]
    [InlineData("tr-TR", "tr")]
    [InlineData("de-DE", "de")]
    [InlineData("nl-NL", "nl")]
    [InlineData("ru-RU", "ru")]
    [InlineData("en-GB", "en")]
    public void Falls_back_to_os_culture_when_no_installer_choice(string os, string expected)
    {
        Assert.Equal(expected, SettingsService.ResolveDefaultLanguage(null, new CultureInfo(os)));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    [InlineData("ja-JP")]
    public void Unsupported_culture_falls_back_to_english(string os)
    {
        Assert.Equal("en", SettingsService.ResolveDefaultLanguage(null, new CultureInfo(os)));
    }

    [Fact]
    public void Unsupported_installer_choice_falls_through_to_os()
    {
        Assert.Equal("tr", SettingsService.ResolveDefaultLanguage("xx", new CultureInfo("tr-TR")));
    }
}
