using System.Collections.ObjectModel;
using BKKleaner.Benchmark;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Optimization;
using BKKleaner.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BKKleaner.ViewModels;

/// <summary>A single optimization action shown as a checkbox in the profile editor / creator.</summary>
public sealed partial class ProfileActionToggle : ObservableObject
{
    public required string ActionId { get; init; }
    public required string TitleKey { get; init; }
    [ObservableProperty] private bool _isIncluded;

    public string Title => Loc.Instance[TitleKey];
    public void RefreshLocalization() => OnPropertyChanged(nameof(Title));
}

public sealed partial class ProfileItemViewModel : ObservableObject
{
    private readonly ProfilesViewModel _parent;
    public GamingProfile Profile { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isEditing;

    public ObservableCollection<ProfileActionToggle> EditableActions { get; } = [];

    public string Name => Profile.IsBuiltIn ? Loc.Instance[Profile.NameKey] : Profile.CustomName ?? "Custom";
    public string Description => Profile.IsBuiltIn ? Loc.Instance[Profile.DescriptionKey] : Loc.Instance["profile.custom.desc"];
    public bool CanDelete => !Profile.IsBuiltIn;
    public bool CanReset => Profile.IsBuiltIn;
    public int ActionCount => Profile.ActionIds.Count;

    public ProfileItemViewModel(GamingProfile profile, ProfilesViewModel parent,
        IReadOnlyList<OptimizationAction> allActions)
    {
        Profile = profile;
        _parent = parent;
        _isActive = profile.IsActive;
        foreach (var action in allActions)
            EditableActions.Add(new ProfileActionToggle
            {
                ActionId = action.Id,
                TitleKey = action.TitleKey,
                IsIncluded = profile.ActionIds.Contains(action.Id)
            });
    }

    [RelayCommand] private Task ApplyAsync() => _parent.ApplyProfileAsync(this);
    [RelayCommand] private Task PreviewAsync() => _parent.PreviewProfileAsync(this);
    [RelayCommand] private void Delete() => _parent.DeleteProfile(this);

    [RelayCommand]
    private void ToggleEdit()
    {
        if (IsEditing) SyncTogglesFromProfile();
        IsEditing = !IsEditing;
    }

    [RelayCommand]
    private void SaveEdit()
    {
        _parent.SaveProfileEdit(this);
        IsEditing = false;
    }

    [RelayCommand] private void ResetEdit() => _parent.ResetProfile(this);

    public IEnumerable<string> SelectedActionIds =>
        EditableActions.Where(a => a.IsIncluded).Select(a => a.ActionId);

    public void SyncTogglesFromProfile()
    {
        foreach (var toggle in EditableActions)
            toggle.IsIncluded = Profile.ActionIds.Contains(toggle.ActionId);
        OnPropertyChanged(nameof(ActionCount));
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
        foreach (var toggle in EditableActions) toggle.RefreshLocalization();
    }
}

public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly IProfileService _profiles;
    private readonly IBenchmarkService _benchmark;
    private readonly INotificationService _notifications;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private string? _comparisonText;
    [ObservableProperty] private bool _runBenchmarkComparison = true;

    // Preview panel (overlay)
    [ObservableProperty] private bool _isPreviewOpen;
    [ObservableProperty] private string? _previewTitle;
    [ObservableProperty] private string? _previewText;

    // Create panel (overlay)
    [ObservableProperty] private bool _isCreateOpen;
    [ObservableProperty] private string _newProfileName = string.Empty;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];
    public ObservableCollection<ProfileActionToggle> NewProfileActions { get; } = [];

    public ProfilesViewModel(IProfileService profiles, IBenchmarkService benchmark,
        INotificationService notifications, ILocalizationService localization)
    {
        _profiles = profiles;
        _benchmark = benchmark;
        _notifications = notifications;
        RebuildProfiles();

        _profiles.ProfilesChanged += (_, _) => RebuildProfiles();
        localization.LanguageChanged += (_, _) =>
        {
            foreach (var p in Profiles) p.RefreshLocalization();
            foreach (var a in NewProfileActions) a.RefreshLocalization();
        };
    }

    private void RebuildProfiles()
    {
        Profiles.Clear();
        foreach (var profile in _profiles.Profiles)
            Profiles.Add(new ProfileItemViewModel(profile, this, _profiles.AvailableActions));
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
            _notifications.Notify(Loc.Instance["profiles.title"], $"{item.Name}: {StatusText}",
                ok ? NotificationLevel.Success : NotificationLevel.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PreviewProfileAsync(ProfileItemViewModel item)
    {
        var previews = await _profiles.PreviewAsync(item.Profile.Id);
        PreviewTitle = item.Name;
        PreviewText = previews.Count == 0
            ? Loc.Instance["profiles.no_changes"]
            : string.Join(Environment.NewLine, previews.SelectMany(p => p.Changes));
        IsPreviewOpen = true;
    }

    [RelayCommand] private void ClosePreview() => IsPreviewOpen = false;

    public void SaveProfileEdit(ProfileItemViewModel item)
    {
        _profiles.UpdateProfileActions(item.Profile.Id, item.SelectedActionIds);
        item.SyncTogglesFromProfile();
        StatusText = $"{item.Name}: {Loc.Instance["profiles.saved"]}";
    }

    public void ResetProfile(ProfileItemViewModel item)
    {
        _profiles.ResetProfile(item.Profile.Id);
        item.SyncTogglesFromProfile();
        StatusText = $"{item.Name}: {Loc.Instance["profiles.reset"]}";
    }

    public void DeleteProfile(ProfileItemViewModel item)
    {
        if (_profiles.DeleteProfile(item.Profile.Id))
            StatusText = $"{item.Name}: {Loc.Instance["profiles.deleted"]}";
    }

    // ---- create panel --------------------------------------------------------

    [RelayCommand]
    private void OpenCreate()
    {
        NewProfileName = string.Empty;
        NewProfileActions.Clear();
        foreach (var action in _profiles.AvailableActions)
            NewProfileActions.Add(new ProfileActionToggle { ActionId = action.Id, TitleKey = action.TitleKey });
        IsCreateOpen = true;
    }

    [RelayCommand] private void CancelCreate() => IsCreateOpen = false;

    [RelayCommand]
    private void ConfirmCreate()
    {
        var selected = NewProfileActions.Where(a => a.IsIncluded).Select(a => a.ActionId).ToList();
        if (selected.Count == 0) { StatusText = Loc.Instance["profiles.pick_action"]; return; }

        var name = string.IsNullOrWhiteSpace(NewProfileName) ? Loc.Instance["profiles.custom_default"] : NewProfileName;
        _profiles.CreateCustomProfile(name, selected);
        IsCreateOpen = false;
        StatusText = $"{name}: {Loc.Instance["profiles.created"]}";
        _notifications.Notify(Loc.Instance["profiles.title"], StatusText, NotificationLevel.Success);
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
