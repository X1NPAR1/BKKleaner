using BKKleaner.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BKKleaner.Tests;

internal static class TestHelpers
{
    /// <summary>Settings service rooted in a unique temp directory, isolated per test.</summary>
    public static SettingsService CreateSettings(out string dataDir)
    {
        dataDir = Path.Combine(Path.GetTempPath(), "BKKleanerTests", Guid.NewGuid().ToString("N"));
        return new SettingsService(NullLogger<SettingsService>.Instance, dataDir);
    }

    /// <summary>Path to the Localization folder copied next to the test binaries.</summary>
    public static string LocalizationDir =>
        Path.Combine(AppContext.BaseDirectory, "Localization");
}
