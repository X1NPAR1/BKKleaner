using System.IO;
using BKKleaner.Models;
using BKKleaner.Recovery;
using BKKleaner.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BKKleaner.Optimization;

public sealed class ProfileService : IProfileService
{
    private readonly ILogger<ProfileService> _logger;
    private readonly IOptimizationService _optimization;
    private readonly IRecoveryService _recovery;
    private readonly string _statePath;
    private readonly string _customizationPath;
    private readonly string _customProfilesPath;
    private readonly Dictionary<string, List<string>> _defaultActions;
    private readonly List<GamingProfile> _profiles;

    public IReadOnlyList<GamingProfile> Profiles => _profiles;
    public GamingProfile? ActiveProfile => Profiles.FirstOrDefault(p => p.IsActive);
    public IReadOnlyList<OptimizationAction> AvailableActions => _optimization.Actions;

    public event EventHandler? ProfilesChanged;

    public ProfileService(ILogger<ProfileService> logger, IOptimizationService optimization,
        IRecoveryService recovery, ISettingsService settings)
    {
        _logger = logger;
        _optimization = optimization;
        _recovery = recovery;
        _statePath = Path.Combine(settings.DataDirectory, "active-profile.json");
        _customizationPath = Path.Combine(settings.DataDirectory, "profile-customizations.json");
        _customProfilesPath = Path.Combine(settings.DataDirectory, "custom-profiles.json");

        _profiles =
        [
            new()
            {
                Id = "competitive_fps", NameKey = "profile.competitive.name", DescriptionKey = "profile.competitive.desc",
                ActionIds = ["power_high_performance", "game_mode", "scheduling_gaming", "latency_responsiveness", "cpu_priority_games", "network_nagle"]
            },
            new()
            {
                Id = "maximum_fps", NameKey = "profile.maxfps.name", DescriptionKey = "profile.maxfps.desc",
                ActionIds = ["power_high_performance", "game_mode", "background_apps", "scheduling_gaming", "latency_responsiveness", "gpu_scheduling", "cpu_priority_games", "visual_effects_performance"]
            },
            new()
            {
                Id = "ultimate_performance", NameKey = "profile.ultimate.name", DescriptionKey = "profile.ultimate.desc",
                ActionIds = ["power_ultimate", "game_mode", "background_apps", "scheduling_gaming", "latency_responsiveness", "gpu_scheduling", "cpu_priority_games", "visual_effects_performance", "menu_show_delay", "disable_transparency", "network_nagle", "telemetry_reduce"]
            },
            new()
            {
                Id = "balanced", NameKey = "profile.balanced.name", DescriptionKey = "profile.balanced.desc",
                ActionIds = ["game_mode", "latency_responsiveness"]
            },
            new()
            {
                Id = "streaming", NameKey = "profile.streaming.name", DescriptionKey = "profile.streaming.desc",
                ActionIds = ["power_high_performance", "game_mode", "scheduling_gaming", "network_nagle"]
            },
            new()
            {
                Id = "low_end", NameKey = "profile.lowend.name", DescriptionKey = "profile.lowend.desc",
                ActionIds = ["power_high_performance", "background_apps", "game_mode", "cpu_priority_games", "visual_effects_performance", "menu_show_delay", "disable_transparency", "telemetry_reduce"]
            },
            new()
            {
                Id = "laptop", NameKey = "profile.laptop.name", DescriptionKey = "profile.laptop.desc",
                ActionIds = ["game_mode", "background_apps", "visual_effects_performance"]
            },
            new()
            {
                Id = "battery_saver", NameKey = "profile.battery.name", DescriptionKey = "profile.battery.desc",
                ActionIds = ["power_saver", "background_apps", "visual_effects_performance", "disable_transparency", "telemetry_reduce"]
            }
        ];

        // Snapshot the built-in action sets so a profile can be reset later.
        _defaultActions = _profiles.ToDictionary(p => p.Id, p => new List<string>(p.ActionIds));

        LoadCustomProfiles();
        ApplyCustomizations();
        RestoreActiveState();
    }

    // ---- custom profiles ------------------------------------------------------

    public GamingProfile CreateCustomProfile(string name, IEnumerable<string> actionIds)
    {
        var valid = actionIds.Where(id => _optimization.Actions.Any(a => a.Id == id)).Distinct().ToList();
        var profile = new GamingProfile
        {
            Id = "custom_" + Guid.NewGuid().ToString("N")[..8],
            NameKey = string.Empty,
            DescriptionKey = "profile.custom.desc",
            ActionIds = valid,
            IsBuiltIn = false,
            CustomName = string.IsNullOrWhiteSpace(name) ? "Custom" : name.Trim()
        };
        _profiles.Add(profile);
        SaveCustomProfiles();
        _logger.LogInformation("Custom profile created: {Name} ({Count} actions)", profile.CustomName, valid.Count);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return profile;
    }

    public bool DeleteProfile(string profileId)
    {
        var profile = _profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null || profile.IsBuiltIn) return false;

        _profiles.Remove(profile);
        SaveCustomProfiles();
        _logger.LogInformation("Custom profile deleted: {Id}", profileId);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void SaveCustomProfiles()
    {
        var custom = _profiles.Where(p => !p.IsBuiltIn)
            .Select(p => new CustomProfileDefinition { Id = p.Id, Name = p.CustomName ?? "Custom", ActionIds = p.ActionIds })
            .ToList();
        File.WriteAllText(_customProfilesPath, JsonConvert.SerializeObject(custom, Formatting.Indented));
    }

    private void LoadCustomProfiles()
    {
        try
        {
            if (!File.Exists(_customProfilesPath)) return;
            var defs = JsonConvert.DeserializeObject<List<CustomProfileDefinition>>(File.ReadAllText(_customProfilesPath));
            if (defs is null) return;
            foreach (var d in defs)
            {
                _profiles.Add(new GamingProfile
                {
                    Id = d.Id,
                    NameKey = string.Empty,
                    DescriptionKey = "profile.custom.desc",
                    ActionIds = d.ActionIds.Where(a => _optimization.Actions.Any(x => x.Id == a)).ToList(),
                    IsBuiltIn = false,
                    CustomName = d.Name
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load custom profiles");
        }
    }

    public async Task<IReadOnlyList<ActionPreview>> PreviewAsync(string profileId)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null) return [];

        var previews = new List<ActionPreview>();
        foreach (var actionId in profile.ActionIds)
            previews.Add(await _optimization.PreviewAsync(actionId).ConfigureAwait(false));
        return previews;
    }

    public async Task<bool> ApplyAsync(string profileId, CancellationToken ct = default)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null)
        {
            _logger.LogWarning("Unknown profile: {Id}", profileId);
            return false;
        }

        // Mandatory recovery step before any optimization batch.
        await _recovery.CreateFullBackupAsync($"Profile '{profileId}'", ct).ConfigureAwait(false);

        // Switching profiles first rolls the previous one back.
        if (ActiveProfile is { } current && current.Id != profileId)
            await UndoActiveAsync(ct).ConfigureAwait(false);

        var allOk = true;
        foreach (var actionId in profile.ActionIds)
        {
            ct.ThrowIfCancellationRequested();
            if (!await _optimization.ApplyAsync(actionId, ct).ConfigureAwait(false))
                allOk = false;
        }

        foreach (var p in Profiles) p.IsActive = false;
        profile.IsActive = true;
        SaveActiveState(profile.Id);
        _logger.LogInformation("Profile {Id} applied (success: {Ok})", profileId, allOk);
        return allOk;
    }

    public async Task<bool> UndoActiveAsync(CancellationToken ct = default)
    {
        var profile = ActiveProfile;
        if (profile is null) return false;

        var allOk = true;
        foreach (var actionId in profile.ActionIds)
        {
            if (!await _optimization.UndoAsync(actionId, ct).ConfigureAwait(false))
                allOk = false;
        }

        profile.IsActive = false;
        SaveActiveState(null);
        _logger.LogInformation("Profile {Id} rolled back", profile.Id);
        return allOk;
    }

    public void UpdateProfileActions(string profileId, IEnumerable<string> actionIds)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null) return;

        var valid = actionIds
            .Where(id => _optimization.Actions.Any(a => a.Id == id))
            .Distinct()
            .ToList();
        profile.ActionIds = valid;
        SaveCustomizations();
        _logger.LogInformation("Profile {Id} customized with {Count} actions", profileId, valid.Count);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetProfile(string profileId)
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
        if (profile is null || !_defaultActions.TryGetValue(profileId, out var defaults)) return;

        profile.ActionIds = new List<string>(defaults);
        SaveCustomizations();
        _logger.LogInformation("Profile {Id} reset to defaults", profileId);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- persistence ----------------------------------------------------------

    private void SaveCustomizations()
    {
        // Only persist built-in profiles whose action set differs from the default.
        var custom = Profiles
            .Where(p => p.IsBuiltIn && _defaultActions.TryGetValue(p.Id, out var def) && !p.ActionIds.SequenceEqual(def))
            .ToDictionary(p => p.Id, p => p.ActionIds);
        File.WriteAllText(_customizationPath, JsonConvert.SerializeObject(custom, Formatting.Indented));

        // Custom profiles persist separately (their action set IS their definition).
        SaveCustomProfiles();
    }

    private void ApplyCustomizations()
    {
        try
        {
            if (!File.Exists(_customizationPath)) return;
            var custom = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(
                File.ReadAllText(_customizationPath));
            if (custom is null) return;

            foreach (var (id, actions) in custom)
            {
                var profile = Profiles.FirstOrDefault(p => p.Id == id);
                if (profile is not null)
                    profile.ActionIds = actions.Where(a => _optimization.Actions.Any(x => x.Id == a)).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load profile customizations");
        }
    }

    private void SaveActiveState(string? profileId) =>
        File.WriteAllText(_statePath, JsonConvert.SerializeObject(new { active = profileId }));

    private void RestoreActiveState()
    {
        try
        {
            if (!File.Exists(_statePath)) return;
            var state = JsonConvert.DeserializeAnonymousType(File.ReadAllText(_statePath), new { active = (string?)null });
            var profile = Profiles.FirstOrDefault(p => p.Id == state?.active);
            if (profile is not null) profile.IsActive = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not restore active profile state");
        }
    }
}
