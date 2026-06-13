using BKKleaner.Models;

namespace BKKleaner.Services;

public interface IRamCleanerService
{
    /// <summary>
    /// Safely trims process working sets, purges the standby list and
    /// flushes the system file cache. Never touches protected system processes.
    /// </summary>
    Task<RamCleanResult> CleanAsync(bool trimWorkingSets, bool clearStandbyList,
        bool optimizeCache, CancellationToken ct = default);
}
