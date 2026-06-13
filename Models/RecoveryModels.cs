namespace BKKleaner.Models;

public enum RecoveryPointKind
{
    SystemRestorePoint,
    RegistryBackup,
    ConfigBackup,
    Snapshot
}

public sealed class RecoveryPoint
{
    public required string Id { get; init; }
    public required RecoveryPointKind Kind { get; init; }
    public required string Description { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string? Path { get; init; }
}
