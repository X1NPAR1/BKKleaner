using BKKleaner.Models;

namespace BKKleaner.Services;

public interface ITempCleanerService
{
    /// <summary>Scans cleanable items for the given mode without deleting anything.</summary>
    Task<IReadOnlyList<TempCleanItem>> ScanAsync(CleanMode mode, CancellationToken ct = default);

    /// <summary>
    /// Moves the selected items into a quarantine folder (restorable) and reports totals.
    /// Locked files are skipped, never forced.
    /// </summary>
    Task<CleaningResult> CleanAsync(IEnumerable<TempCleanItem> items, CleanMode mode, CancellationToken ct = default);

    /// <summary>Lists existing quarantine snapshots that can be restored.</summary>
    IReadOnlyList<string> GetQuarantineSnapshots();

    /// <summary>Restores every file of a quarantine snapshot to its original location.</summary>
    Task<int> RestoreAsync(string snapshotId, CancellationToken ct = default);

    /// <summary>Permanently removes quarantine snapshots older than the given age.</summary>
    int PurgeQuarantine(TimeSpan olderThan);
}
