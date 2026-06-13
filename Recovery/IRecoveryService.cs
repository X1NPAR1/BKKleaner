using BKKleaner.Models;

namespace BKKleaner.Recovery;

public interface IRecoveryService
{
    IReadOnlyList<RecoveryPoint> GetRecoveryPoints();

    /// <summary>Creates a Windows System Restore point (best effort, frequency-limited by the OS).</summary>
    Task<RecoveryPoint?> CreateRestorePointAsync(string description, CancellationToken ct = default);

    /// <summary>Exports a registry key to a timestamped .reg file.</summary>
    Task<RecoveryPoint?> BackupRegistryKeyAsync(string keyPath, CancellationToken ct = default);

    /// <summary>Copies the current application configuration into the backup store.</summary>
    Task<RecoveryPoint?> BackupConfigAsync(CancellationToken ct = default);

    /// <summary>Serializes the current optimization state into a snapshot.</summary>
    Task<RecoveryPoint?> CreateSnapshotAsync(CancellationToken ct = default);

    /// <summary>Runs every backup type at once — called automatically before optimizations.</summary>
    Task<IReadOnlyList<RecoveryPoint>> CreateFullBackupAsync(string reason, CancellationToken ct = default);

    /// <summary>One-click restore of a recovery point (registry import / config copy-back).</summary>
    Task<bool> RestoreAsync(RecoveryPoint point, CancellationToken ct = default);
}
