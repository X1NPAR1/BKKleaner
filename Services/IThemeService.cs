namespace BKKleaner.Services;

public interface IThemeService
{
    IReadOnlyList<string> AvailableThemes { get; }
    string CurrentTheme { get; }
    event EventHandler? ThemeChanged;

    /// <summary>Switches the application theme at runtime without restart.</summary>
    bool ApplyTheme(string themeName);

    /// <summary>Loads a user-supplied JSON color map as a custom theme.</summary>
    bool ApplyCustomTheme(string jsonPath);
}
