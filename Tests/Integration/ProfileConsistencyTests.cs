using BKKleaner.Optimization;
using BKKleaner.Recovery;
using BKKleaner.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Integration;

public class ProfileConsistencyTests
{
    private static (ProfileService profiles, OptimizationService optimization) Create()
    {
        var settings = TestHelpers.CreateSettings(out _);
        var security = new SecurityService(NullLogger<SecurityService>.Instance);
        var recovery = new RecoveryService(NullLogger<RecoveryService>.Instance, security, settings);
        var optimization = new OptimizationService(
            NullLogger<OptimizationService>.Instance, security, recovery, settings);
        var profiles = new ProfileService(
            NullLogger<ProfileService>.Instance, optimization, recovery, settings);
        return (profiles, optimization);
    }

    [Fact]
    public void All_required_profiles_exist_including_ultimate_and_battery()
    {
        var (profiles, _) = Create();
        Assert.Equal(
            ["competitive_fps", "maximum_fps", "ultimate_performance", "balanced",
             "streaming", "low_end", "laptop", "battery_saver"],
            profiles.Profiles.Select(p => p.Id));
    }

    [Fact]
    public void Every_profile_action_maps_to_a_real_optimization_action()
    {
        var (profiles, optimization) = Create();
        var known = optimization.Actions.Select(a => a.Id).ToHashSet();
        foreach (var profile in profiles.Profiles)
        {
            Assert.NotEmpty(profile.ActionIds);
            Assert.All(profile.ActionIds, id => Assert.Contains(id, known));
        }
    }

    [Fact]
    public async Task Profile_preview_describes_changes_without_applying()
    {
        var (profiles, optimization) = Create();
        var previews = await profiles.PreviewAsync("balanced");
        Assert.NotEmpty(previews);
        Assert.All(previews, p => Assert.NotEmpty(p.Changes));
        // Nothing was applied.
        Assert.All(optimization.Actions, a => Assert.False(a.IsApplied));
    }

    [Fact]
    public void No_profile_is_active_by_default()
    {
        var (profiles, _) = Create();
        Assert.Null(profiles.ActiveProfile);
    }

    [Fact]
    public void Editing_a_profile_persists_and_can_be_reset()
    {
        var (profiles, _) = Create();
        var profile = profiles.Profiles.First(p => p.Id == "balanced");
        var original = profile.ActionIds.ToList();

        profiles.UpdateProfileActions("balanced", ["game_mode"]);
        Assert.Equal(["game_mode"], profile.ActionIds);

        profiles.ResetProfile("balanced");
        Assert.Equal(original, profile.ActionIds);
    }

    [Fact]
    public void Editing_ignores_unknown_action_ids()
    {
        var (profiles, _) = Create();
        profiles.UpdateProfileActions("balanced", ["game_mode", "not_a_real_action"]);
        Assert.Equal(["game_mode"], profiles.Profiles.First(p => p.Id == "balanced").ActionIds);
    }

    [Fact]
    public void Profile_customization_changed_event_fires()
    {
        var (profiles, _) = Create();
        var fired = false;
        profiles.ProfilesChanged += (_, _) => fired = true;
        profiles.UpdateProfileActions("laptop", ["game_mode"]);
        Assert.True(fired);
    }

    [Fact]
    public void Create_and_delete_custom_profile()
    {
        var (profiles, _) = Create();
        var before = profiles.Profiles.Count;

        var custom = profiles.CreateCustomProfile("My FPS", ["game_mode", "latency_responsiveness"]);
        Assert.False(custom.IsBuiltIn);
        Assert.Equal("My FPS", custom.CustomName);
        Assert.Equal(before + 1, profiles.Profiles.Count);
        Assert.Equal(["game_mode", "latency_responsiveness"], custom.ActionIds);

        Assert.True(profiles.DeleteProfile(custom.Id));
        Assert.Equal(before, profiles.Profiles.Count);
    }

    [Fact]
    public void Built_in_profiles_cannot_be_deleted()
    {
        var (profiles, _) = Create();
        Assert.False(profiles.DeleteProfile("balanced"));
        Assert.Contains(profiles.Profiles, p => p.Id == "balanced");
    }

    [Fact]
    public void Custom_profiles_persist_across_reloads()
    {
        var settings = TestHelpers.CreateSettings(out _);
        var security = new SecurityService(NullLogger<SecurityService>.Instance);
        var recovery = new RecoveryService(NullLogger<RecoveryService>.Instance, security, settings);
        var optimization = new OptimizationService(NullLogger<OptimizationService>.Instance, security, recovery, settings);

        var first = new ProfileService(NullLogger<ProfileService>.Instance, optimization, recovery, settings);
        first.CreateCustomProfile("Persisted", ["game_mode"]);

        var reloaded = new ProfileService(NullLogger<ProfileService>.Instance, optimization, recovery, settings);
        Assert.Contains(reloaded.Profiles, p => p.CustomName == "Persisted" && !p.IsBuiltIn);
    }

    [Fact]
    public void Power_plans_are_registered_as_mutually_exclusive()
    {
        var (_, optimization) = Create();
        Assert.Equal(
            ["power_high_performance", "power_saver", "power_ultimate"],
            optimization.PowerPlanActionIds.OrderBy(x => x));
    }
}
