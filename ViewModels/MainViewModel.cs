using System.Collections.ObjectModel;
using BKKleaner.Localization;
using BKKleaner.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class NavigationItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Icon { get; init; }
    public required object ViewModel { get; init; }

    [ObservableProperty] private bool _isSelected;

    public string Title => Loc.Instance[$"nav.{Key}"];
    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));
}

public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private object? _currentViewModel;
    [ObservableProperty] private string _adminBadge;

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

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
        ILocalizationService localization)
    {
        NavigationItems.Add(new NavigationItem { Key = "dashboard", Icon = "", ViewModel = dashboard });
        NavigationItems.Add(new NavigationItem { Key = "monitoring", Icon = "", ViewModel = monitoring });
        NavigationItems.Add(new NavigationItem { Key = "optimization", Icon = "", ViewModel = optimization });
        NavigationItems.Add(new NavigationItem { Key = "ram", Icon = "", ViewModel = ramCleaner });
        NavigationItems.Add(new NavigationItem { Key = "temp", Icon = "", ViewModel = tempCleaner });
        NavigationItems.Add(new NavigationItem { Key = "profiles", Icon = "", ViewModel = profiles });
        NavigationItems.Add(new NavigationItem { Key = "benchmark", Icon = "", ViewModel = benchmark });
        NavigationItems.Add(new NavigationItem { Key = "recovery", Icon = "", ViewModel = recovery });
        NavigationItems.Add(new NavigationItem { Key = "settings", Icon = "", ViewModel = settings });
        NavigationItems.Add(new NavigationItem { Key = "logs", Icon = "", ViewModel = logs });
        NavigationItems.Add(new NavigationItem { Key = "updates", Icon = "", ViewModel = updates });

        _adminBadge = security.IsAdministrator ? "Administrator" : "Limited";
        localization.LanguageChanged += (_, _) =>
        {
            foreach (var item in NavigationItems) item.RefreshLocalization();
        };

        Navigate(NavigationItems[0]);
    }

    [RelayCommand]
    private void Navigate(NavigationItem item)
    {
        foreach (var nav in NavigationItems) nav.IsSelected = ReferenceEquals(nav, item);
        CurrentViewModel = item.ViewModel;
    }
}
