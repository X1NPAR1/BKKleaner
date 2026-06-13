using BKKleaner.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class SettingsServiceTests
{
    [Fact]
    public void Defaults_are_sensible()
    {
        var svc = TestHelpers.CreateSettings(out _);
        Assert.Equal("en", svc.Current.Language);
        Assert.Equal("Dark", svc.Current.Theme);
        Assert.True(svc.Current.CreateRestorePointBeforeOptimization);
    }

    [Fact]
    public void Update_persists_and_reloads()
    {
        var svc = TestHelpers.CreateSettings(out var dir);
        svc.Update(s => { s.Language = "tr"; s.Theme = "Gaming"; s.MonitoringIntervalMs = 2000; });

        var reloaded = new SettingsService(NullLogger<SettingsService>.Instance, dir);
        Assert.Equal("tr", reloaded.Current.Language);
        Assert.Equal("Gaming", reloaded.Current.Theme);
        Assert.Equal(2000, reloaded.Current.MonitoringIntervalMs);
    }

    [Fact]
    public void Update_raises_changed_event()
    {
        var svc = TestHelpers.CreateSettings(out _);
        var raised = false;
        svc.SettingsChanged += (_, _) => raised = true;
        svc.Update(s => s.Language = "de");
        Assert.True(raised);
    }

    [Fact]
    public void Corrupt_settings_file_falls_back_to_defaults()
    {
        var svc = TestHelpers.CreateSettings(out var dir);
        svc.Save();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{ not valid json !!");

        var reloaded = new SettingsService(NullLogger<SettingsService>.Instance, dir);
        Assert.Equal("en", reloaded.Current.Language);
    }
}
