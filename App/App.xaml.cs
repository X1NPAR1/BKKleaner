using System.IO;
using System.Windows;
using System.Windows.Threading;
using BKKleaner.Benchmark;
using BKKleaner.Localization;
using BKKleaner.Monitoring;
using BKKleaner.Optimization;
using BKKleaner.Recovery;
using BKKleaner.Security;
using BKKleaner.Services;
using BKKleaner.ViewModels;
using BKKleaner.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace BKKleaner.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private Microsoft.Extensions.Logging.ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logStore = new LogStore();
        ConfigureSerilog(logStore);

        _services = BuildServices(logStore);
        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("BKKleaner starting (admin: {Admin})",
            _services.GetRequiredService<ISecurityService>().IsAdministrator);

        HookGlobalExceptionHandlers();

        // Localization + theme from persisted settings.
        var localization = _services.GetRequiredService<ILocalizationService>();
        Loc.Initialize(localization);
        var settings = _services.GetRequiredService<ISettingsService>();
        localization.SetLanguage(settings.Current.Language);

        var themes = _services.GetRequiredService<IThemeService>();
        if (settings.Current.Theme == "Custom" && settings.Current.CustomThemePath is { } customPath
            && File.Exists(customPath))
            themes.ApplyCustomTheme(customPath);
        else
            themes.ApplyTheme(settings.Current.Theme);

        // Global animation switch, kept in sync with the setting.
        UI.AnimationSettings.Enabled = settings.Current.EnableAnimations;
        settings.SettingsChanged += (_, _) =>
            UI.AnimationSettings.Enabled = settings.Current.EnableAnimations;

        // Start monitoring before the dashboard appears.
        _services.GetRequiredService<IHardwareMonitoringService>().Start();

        // Start the automatic RAM-clean scheduler (reads its own settings).
        _services.GetRequiredService<IAutoRamCleanService>();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // webhook.txt automation runs after launch, off the UI thread.
        _ = ProcessWebhookFileAsync();
    }

    private async Task ProcessWebhookFileAsync()
    {
        try
        {
            var automation = _services!.GetRequiredService<IWebhookAutomationService>();
            var report = await automation.ProcessStartupFileAsync().ConfigureAwait(false);
            if (report.Executed.Count + report.Rejected.Count + report.Failed.Count > 0)
                _logger?.LogInformation(
                    "webhook.txt: {Executed} executed, {Rejected} rejected, {Failed} failed",
                    report.Executed.Count, report.Rejected.Count, report.Failed.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "webhook.txt automation failed");
        }
    }

    private static void ConfigureSerilog(ILogStore logStore)
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BKKleaner", "Logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(logDir, "bkkleaner-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14)
            .WriteTo.Sink(new LogStoreSink(logStore))
            .CreateLogger();
    }

    private static ServiceProvider BuildServices(ILogStore logStore)
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSerilog(dispose: true));
        services.AddSingleton(logStore);

        // Core services
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IHardwareMonitoringService, HardwareMonitoringService>();
        services.AddSingleton<IRamCleanerService, RamCleanerService>();
        services.AddSingleton<ITempCleanerService, TempCleanerService>();
        services.AddSingleton<IRecoveryService, RecoveryService>();
        services.AddSingleton<IOptimizationService, OptimizationService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IBenchmarkService, BenchmarkService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IWebhookAutomationService, WebhookAutomationService>();
        services.AddSingleton<IAutoRamCleanService, AutoRamCleanService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<MonitoringViewModel>();
        services.AddSingleton<OptimizationViewModel>();
        services.AddSingleton<RamCleanerViewModel>();
        services.AddSingleton<TempCleanerViewModel>();
        services.AddSingleton<ProfilesViewModel>();
        services.AddSingleton<BenchmarkViewModel>();
        services.AddSingleton<RecoveryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<UpdatesViewModel>();
        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unhandled UI exception");
            MessageBox.Show(args.Exception.Message, "BKKleaner — Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                _logger?.LogCritical(ex, "Unhandled domain exception");
            Log.CloseAndFlush();
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _services?.GetService<IHardwareMonitoringService>()?.Dispose();
            _services?.Dispose();
        }
        finally
        {
            Log.CloseAndFlush();
        }
        base.OnExit(e);
    }
}
