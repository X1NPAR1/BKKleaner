using BKKleaner.Benchmark;
using BKKleaner.Localization;
using BKKleaner.Monitoring;
using BKKleaner.Optimization;
using BKKleaner.Recovery;
using BKKleaner.Security;
using BKKleaner.Services;
using BKKleaner.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Ui;

/// <summary>UI behaviour tests at the ViewModel layer (navigation, theme switching).</summary>
public class NavigationAndThemeTests
{
    private static MainViewModel CreateMainViewModel(out IThemeService themes, out ILocalizationService localization)
    {
        var settings = TestHelpers.CreateSettings(out _);
        var security = new SecurityService(NullLogger<SecurityService>.Instance);
        localization = new LocalizationService(NullLogger<LocalizationService>.Instance, TestHelpers.LocalizationDir);
        themes = new ThemeService(NullLogger<ThemeService>.Instance, settings);
        var monitoring = new HardwareMonitoringService(NullLogger<HardwareMonitoringService>.Instance, settings);
        var recovery = new RecoveryService(NullLogger<RecoveryService>.Instance, security, settings);
        var optimization = new OptimizationService(NullLogger<OptimizationService>.Instance, security, recovery, settings);
        var profiles = new ProfileService(NullLogger<ProfileService>.Instance, optimization, recovery, settings);
        var benchmark = new BenchmarkService(NullLogger<BenchmarkService>.Instance, monitoring, settings);
        var tempCleaner = new TempCleanerService(NullLogger<TempCleanerService>.Instance, settings);
        var ramCleaner = new RamCleanerService(NullLogger<RamCleanerService>.Instance, security);
        var updates = new UpdateService(NullLogger<UpdateService>.Instance, security);
        var logStore = new LogStore();
        var autoRam = new AutoRamCleanService(NullLogger<AutoRamCleanService>.Instance, settings, ramCleaner);
        var systemInfo = new SystemInfoService(NullLogger<SystemInfoService>.Instance);
        var navigation = new NavigationService();

        return new MainViewModel(
            new DashboardViewModel(monitoring, optimization, ramCleaner, tempCleaner, systemInfo, navigation),
            new MonitoringViewModel(monitoring),
            new OptimizationViewModel(optimization, recovery, localization),
            new RamCleanerViewModel(ramCleaner, settings, autoRam),
            new TempCleanerViewModel(tempCleaner),
            new ProfilesViewModel(profiles, benchmark, localization),
            new BenchmarkViewModel(benchmark),
            new RecoveryViewModel(recovery),
            new SettingsViewModel(settings, themes, localization),
            new LogsViewModel(logStore),
            new UpdatesViewModel(updates),
            security,
            settings,
            ramCleaner,
            navigation,
            localization);
    }

    [Fact]
    public void Sidebar_has_all_eleven_pages()
    {
        var vm = CreateMainViewModel(out _, out _);
        Assert.Equal(
            ["dashboard", "monitoring", "optimization", "ram", "temp", "profiles",
             "benchmark", "recovery", "settings", "logs", "updates"],
            vm.NavigationItems.Select(n => n.Key));
    }

    [Fact]
    public void Startup_selects_dashboard()
    {
        var vm = CreateMainViewModel(out _, out _);
        Assert.True(vm.NavigationItems[0].IsSelected);
        Assert.IsType<DashboardViewModel>(vm.CurrentViewModel);
    }

    [Fact]
    public void Navigation_switches_current_view_model_and_selection()
    {
        var vm = CreateMainViewModel(out _, out _);
        var target = vm.NavigationItems.First(n => n.Key == "benchmark");

        vm.NavigateCommand.Execute(target);

        Assert.IsType<BenchmarkViewModel>(vm.CurrentViewModel);
        Assert.True(target.IsSelected);
        Assert.Equal(1, vm.NavigationItems.Count(n => n.IsSelected));
    }

    [Fact]
    public void Theme_switching_updates_state_and_persists()
    {
        CreateMainViewModel(out var themes, out _);
        Assert.True(themes.ApplyTheme("Gaming"));
        Assert.Equal("Gaming", themes.CurrentTheme);
        Assert.False(themes.ApplyTheme("NoSuchTheme"));
        Assert.Equal("Gaming", themes.CurrentTheme);
    }

    [Fact]
    public void All_five_built_in_themes_are_available()
    {
        CreateMainViewModel(out var themes, out _);
        Assert.Equal(["Light", "Dark", "Gaming", "RgbNeon", "Minimal"], themes.AvailableThemes);
    }

    [Fact]
    public void Theme_xaml_files_exist_for_every_built_in_theme()
    {
        CreateMainViewModel(out var themes, out _);
        // Walk up from the test output directory to the repository root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Themes")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var themesDir = Path.Combine(dir!.FullName, "Themes");
        Assert.All(themes.AvailableThemes,
            t => Assert.True(File.Exists(Path.Combine(themesDir, $"{t}.xaml")), $"{t}.xaml missing"));
    }

    [Fact]
    public void Language_switch_refreshes_navigation_titles()
    {
        var vm = CreateMainViewModel(out _, out var localization);
        Loc.Initialize(localization);
        localization.SetLanguage("tr");
        var dashboard = vm.NavigationItems.First(n => n.Key == "dashboard");
        Assert.Equal("Gösterge Paneli", dashboard.Title);
    }
}
