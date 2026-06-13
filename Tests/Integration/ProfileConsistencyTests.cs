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
    public void Exactly_six_required_profiles_exist()
    {
        var (profiles, _) = Create();
        Assert.Equal(
            ["competitive_fps", "maximum_fps", "balanced", "streaming", "low_end", "laptop"],
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
}
