using System.Collections.ObjectModel;
using System.Windows;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Monitoring;
using BKKleaner.Security;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class NavigationItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Glyph { get; init; }
    public required object ViewModel { get; init; }

    [ObservableProperty] private bool _isSelected;

    public string Title => Loc.Instance[$"nav.{Key}"];
    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IRamCleanerService _ramCleaner;
    private readonly INotificationService _notifications;

    public System.Collections.ObjectModel.ReadOnlyObservableCollection<ToastNotification> Toasts => _notifications.Active;

    [RelayCommand]
    private void DismissToast(ToastNotification toast) => _notifications.Dismiss(toast);

    [ObservableProperty] private object? _currentViewModel;
    [ObservableProperty] private string _adminBadge;
    [ObservableProperty] private bool _isAdministrator;
    [ObservableProperty] private string _version;
    [ObservableProperty] private string? _trayStatus;

    // Live values surfaced in the tray tooltip.
    [ObservableProperty] private double _trayCpuTemp;
    [ObservableProperty] private double _trayCpuUsage;
    [ObservableProperty] private double _trayGpuUsage;
    [ObservableProperty] private double _trayRamUsage;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    /// <summary>Raised by the tray "Open" action / double-click.</summary>
    public event EventHandler? ShowRequested;

    /// <summary>Raised by the tray "Exit" action to really close the app.</summary>
    public event EventHandler? ExitRequested;

    public MainViewModel(
        DashboardViewModel dashboard,
        MonitoringViewModel monitoring,
        OptimizationViewModel optimization,
        RamCleanerViewModel ramCleaner,
        TempCleanerViewModel tempCleaner,
        ProfilesViewModel profiles,
        BenchmarkViewModel benchmark,
        RecoveryViewModel recovery,
        SettingsViewModel settings,
        LogsViewModel logs,
        UpdatesViewModel updates,
        ISecurityService security,
        ISettingsService settingsService,
        IRamCleanerService ramCleanerService,
        INavigationService navigation,
        IHardwareMonitoringService hardwareMonitoring,
        INotificationService notifications,
        ILocalizationService localization)
    {
        _settings = settingsService;
        _ramCleaner = ramCleanerService;
        _notifications = notifications;
        navigation.NavigationRequested += (_, key) => NavigateToKey(key);
        hardwareMonitoring.SnapshotUpdated += OnSnapshot;

        // Segoe MDL2 Assets glyphs for a native Windows 11 look.
        NavigationItems.Add(new NavigationItem { Key = "dashboard", Glyph = "", ViewModel = dashboard });
        NavigationItems.Add(new NavigationItem { Key = "monitoring", Glyph = "", ViewModel = monitoring });
        NavigationItems.Add(new NavigationItem { Key = "optimization", Glyph = "", ViewModel = optimization });
        NavigationItems.Add(new NavigationItem { Key = "ram", Glyph = "", ViewModel = ramCleaner });
        NavigationItems.Add(new NavigationItem { Key = "temp", Glyph = "", ViewModel = tempCleaner });
        NavigationItems.Add(new NavigationItem { Key = "profiles", Glyph = "", ViewModel = profiles });
        NavigationItems.Add(new NavigationItem { Key = "benchmark", Glyph = "", ViewModel = benchmark });
        NavigationItems.Add(new NavigationItem { Key = "recovery", Glyph = "", ViewModel = recovery });
        NavigationItems.Add(new NavigationItem { Key = "settings", Glyph = "", ViewModel = settings });
        NavigationItems.Add(new NavigationItem { Key = "logs", Glyph = "", ViewModel = logs });
        NavigationItems.Add(new NavigationItem { Key = "updates", Glyph = "", ViewModel = updates });

        IsAdministrator = security.IsAdministrator;
        _adminBadge = security.IsAdministrator ? "Administrator" : "Limited";
        _version = $"v{ThisAssemblyVersion()} · X1NPAR1";

        localization.LanguageChanged += (_, _) =>
        {
            foreach (var item in NavigationItems) item.RefreshLocalization();
        };

        Navigate(NavigationItems[0]);
    }

    private void OnSnapshot(object? sender, HardwareSnapshot s)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            TrayCpuTemp = Math.Round(s.Cpu.TemperatureC, 0);
            TrayCpuUsage = Math.Round(s.Cpu.UsagePercent, 0);
            TrayGpuUsage = Math.Round(s.Gpu.UsagePercent, 0);
            TrayRamUsage = Math.Round(s.Ram.UsagePercent, 0);
        });
    }

    private static string ThisAssemblyVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "3.5.2" : $"{v.Major}.{v.Minor}.{v.Build}";
    }

    [RelayCommand]
    private void Navigate(NavigationItem item)
    {
        foreach (var nav in NavigationItems) nav.IsSelected = ReferenceEquals(nav, item);
        CurrentViewModel = item.ViewModel;
    }

    public void NavigateToKey(string key)
    {
        var item = NavigationItems.FirstOrDefault(n => n.Key == key);
        if (item is not null) Navigate(item);
    }

    [RelayCommand]
    private void TrayOpen() => ShowRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void TrayExit() => ExitRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task TrayCleanRam()
    {
        TrayStatus = Loc.Instance["ram.cleaning"];
        var result = await _ramCleaner.CleanAsync(true, true, false);
        TrayStatus = $"{Loc.Instance["ram.freed"]}: {result.FreedMb:0} MB";
    }

    public bool CloseToTray => _settings.Current.CloseToTray;
    public bool MinimizeToTray => _settings.Current.MinimizeToTray;
}
