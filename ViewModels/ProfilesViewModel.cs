using System.Collections.ObjectModel;
using BKKleaner.Benchmark;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Optimization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

public sealed partial class ProfileItemViewModel : ObservableObject
{
    private readonly ProfilesViewModel _parent;
    public GamingProfile Profile { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string? _previewText;

    public string Name => Loc.Instance[Profile.NameKey];
    public string Description => Loc.Instance[Profile.DescriptionKey];

    public ProfileItemViewModel(GamingProfile profile, ProfilesViewModel parent)
    {
        Profile = profile;
        _parent = parent;
        _isActive = profile.IsActive;
    }

    [RelayCommand]
    private Task ApplyAsync() => _parent.ApplyProfileAsync(this);

    [RelayCommand]
    private Task PreviewAsync() => _parent.PreviewProfileAsync(this);

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}

public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly IProfileService _profiles;
    private readonly IBenchmarkService _benchmark;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string? _comparisonText;
    [ObservableProperty] private bool _runBenchmarkComparison = true;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    public ProfilesViewModel(IProfileService profiles, IBenchmarkService benchmark,
        ILocalizationService localization)
    {
        _profiles = profiles;
        _benchmark = benchmark;
        foreach (var profile in profiles.Profiles)
            Profiles.Add(new ProfileItemViewModel(profile, this));
        localization.LanguageChanged += (_, _) =>
        {
            foreach (var p in Profiles) p.RefreshLocalization();
        };
    }

    public async Task ApplyProfileAsync(ProfileItemViewModel item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            BenchmarkResult? before = null;
            if (RunBenchmarkComparison)
            {
                StatusText = Loc.Instance["profiles.benchmarking_before"];
                before = await _benchmark.RunAsync("before");
            }

            StatusText = Loc.Instance["profiles.applying"];
            var ok = await _profiles.ApplyAsync(item.Profile.Id);
            foreach (var p in Profiles) p.IsActive = p.Profile.IsActive;

            if (ok && before is not null)
            {
                StatusText = Loc.Instance["profiles.benchmarking_after"];
                var after = await _benchmark.RunAsync("after");
                var comparison = _benchmark.Compare(before, after);
                var report = await _benchmark.ExportReportAsync(comparison);
                ComparisonText =
                    $"FPS {comparison.FpsDeltaPercent:+0.#;-0.#;0}% · " +
                    $"Latency {comparison.LatencyDeltaPercent:+0.#;-0.#;0}% · {report}";
            }

            StatusText = ok ? Loc.Instance["profiles.applied"] : Loc.Instance["profiles.failed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PreviewProfileAsync(ProfileItemViewModel item)
    {
        var previews = await _profiles.PreviewAsync(item.Profile.Id);
        item.PreviewText = string.Join(Environment.NewLine,
            previews.SelectMany(p => p.Changes));
    }

    [RelayCommand]
    private async Task UndoActiveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _profiles.UndoActiveAsync();
            foreach (var p in Profiles) p.IsActive = p.Profile.IsActive;
            StatusText = Loc.Instance["profiles.rolled_back"];
        }
        finally
        {
            IsBusy = false;
        }
    }
}
