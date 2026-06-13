using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Security;

public sealed class SecurityService : ISecurityService
{
    private readonly ILogger<SecurityService> _logger;
    private readonly Lazy<bool> _isAdmin;

    // Permissions that are only granted to an elevated process.
    private static readonly HashSet<AppPermission> AdminOnly =
    [
        AppPermission.ModifyRegistry,
        AppPermission.ChangePowerPlan,
        AppPermission.CreateRestorePoint,
        AppPermission.CleanRam,
        AppPermission.RunUpdates
    ];

    public SecurityService(ILogger<SecurityService> logger)
    {
        _logger = logger;
        _isAdmin = new Lazy<bool>(static () =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        });
    }

    public bool IsAdministrator => _isAdmin.Value;

    public bool HasPermission(AppPermission permission) =>
        !AdminOnly.Contains(permission) || IsAdministrator;

    public async Task<SafeResult<T>> ExecuteSafeAsync<T>(
        AppPermission permission, string operationName, Func<Task<T>> operation)
    {
        if (!HasPermission(permission))
        {
            _logger.LogWarning("Operation {Operation} rejected: missing permission {Permission}",
                operationName, permission);
            return SafeResult<T>.Fail($"Permission denied: {permission}");
        }

        try
        {
            _logger.LogInformation("Operation {Operation} started", operationName);
            var value = await operation().ConfigureAwait(false);
            _logger.LogInformation("Operation {Operation} completed", operationName);
            return SafeResult<T>.Ok(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Operation {Operation} failed", operationName);
            return SafeResult<T>.Fail(ex.Message);
        }
    }
}
