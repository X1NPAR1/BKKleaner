namespace BKKleaner.Security;

public enum AppPermission
{
    ReadSensors,
    CleanTemp,
    CleanRam,
    ModifyRegistry,
    ChangePowerPlan,
    CreateRestorePoint,
    RunUpdates
}

public interface ISecurityService
{
    bool IsAdministrator { get; }

    /// <summary>True when the current process is allowed to perform the operation.</summary>
    bool HasPermission(AppPermission permission);

    /// <summary>
    /// Runs an operation inside the safe execution layer: permission check,
    /// structured logging and full exception capture. Never throws.
    /// </summary>
    Task<SafeResult<T>> ExecuteSafeAsync<T>(
        AppPermission permission, string operationName, Func<Task<T>> operation);
}

public sealed class SafeResult<T>
{
    public bool Success { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }

    public static SafeResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static SafeResult<T> Fail(string error) => new() { Success = false, Error = error };
}
