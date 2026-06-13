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

    public IReadOnlyList<GamingProfile> Profiles { get; }
    public GamingProfile? ActiveProfile => Profiles.FirstOrDefault(p => p.IsActive);

    public ProfileService(ILogger<ProfileService> logger, IOptimizationService optimization,
        IRecoveryService recovery, ISettingsService settings)
    {
        _logger = logger;
        _optimization = optimization;
        _recovery = recovery;
        _statePath = Path.Combine(settings.DataDirectory, "active-profile.json");

        Profiles =
        [
            new()
            {
                Id = "competitive_fps", NameKey = "profile.competitive.name", DescriptionKey = "profile.competitive.desc",
                ActionIds = ["power_high_performance", "game_mode", "scheduling_gaming", "latency_responsiveness", "cpu_priority_games"]
            },
            new()
            {
                Id = "maximum_fps", NameKey = "profile.maxfps.name", DescriptionKey = "profile.maxfps.desc",
                ActionIds = ["power_high_performance", "game_mode", "background_apps", "scheduling_gaming", "latency_responsiveness", "gpu_scheduling", "cpu_priority_games"]
            },
            new()
            {
                Id = "balanced", NameKey = "profile.balanced.name", DescriptionKey = "profile.balanced.desc",
                ActionIds = ["game_mode", "latency_responsiveness"]
            },
            new()
            {
                Id = "streaming", NameKey = "profile.streaming.name", DescriptionKey = "profile.streaming.desc",
                ActionIds = ["power_high_performance", "game_mode", "scheduling_gaming"]
            },
            new()
            {
                Id = "low_end", NameKey = "profile.lowend.name", DescriptionKey = "profile.lowend.desc",
                ActionIds = ["background_apps", "game_mode", "cpu_priority_games"]
            },
            new()
            {
                Id = "laptop", NameKey = "profile.laptop.name", DescriptionKey = "profile.laptop.desc",
                ActionIds = ["game_mode", "background_apps"]
            }
        ];

        RestoreActiveState();
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
