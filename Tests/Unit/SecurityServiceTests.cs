using BKKleaner.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class SecurityServiceTests
{
    private static SecurityService Create() => new(NullLogger<SecurityService>.Instance);

    [Fact]
    public void Non_admin_permissions_are_always_granted()
    {
        var svc = Create();
        Assert.True(svc.HasPermission(AppPermission.ReadSensors));
        Assert.True(svc.HasPermission(AppPermission.CleanTemp));
    }

    [Fact]
    public void Admin_permissions_match_elevation_state()
    {
        var svc = Create();
        Assert.Equal(svc.IsAdministrator, svc.HasPermission(AppPermission.ModifyRegistry));
        Assert.Equal(svc.IsAdministrator, svc.HasPermission(AppPermission.ChangePowerPlan));
    }

    [Fact]
    public async Task Safe_execution_returns_value_on_success()
    {
        var svc = Create();
        var result = await svc.ExecuteSafeAsync(AppPermission.ReadSensors, "test",
            () => Task.FromResult(42));
        Assert.True(result.Success);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task Safe_execution_never_throws()
    {
        var svc = Create();
        var result = await svc.ExecuteSafeAsync<int>(AppPermission.ReadSensors, "test",
            () => throw new InvalidOperationException("boom"));
        Assert.False(result.Success);
        Assert.Equal("boom", result.Error);
    }
}
