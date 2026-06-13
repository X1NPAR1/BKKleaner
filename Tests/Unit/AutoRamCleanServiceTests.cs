using BKKleaner.Services;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class AutoRamCleanServiceTests
{
    [Fact]
    public void Allowed_intervals_match_the_specification()
    {
        Assert.Equal([5, 10, 15, 25, 30, 45, 60, 120],
            IAutoRamCleanService.AllowedIntervalsMinutes);
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(7, 5)]
    [InlineData(13, 15)]
    [InlineData(20, 15)]
    [InlineData(23, 25)]
    [InlineData(50, 45)]
    [InlineData(90, 60)]
    [InlineData(1000, 120)]
    [InlineData(0, 5)]
    public void Snap_interval_picks_nearest_allowed(int requested, int expected)
    {
        Assert.Equal(expected, AutoRamCleanService.SnapInterval(requested));
    }

    [Fact]
    public void Every_allowed_interval_snaps_to_itself()
    {
        foreach (var interval in IAutoRamCleanService.AllowedIntervalsMinutes)
            Assert.Equal(interval, AutoRamCleanService.SnapInterval(interval));
    }
}
