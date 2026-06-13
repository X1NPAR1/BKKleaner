using BKKleaner.Models;

namespace BKKleaner.Services;

public interface IUpdateService
{
    /// <summary>Collects available updates from winget plus runtime/redist checks.</summary>
    Task<IReadOnlyList<UpdateItem>> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Upgrades a single winget package silently; validates the exit code.</summary>
    Task<bool> UpgradeAsync(UpdateItem item, CancellationToken ct = default);

    /// <summary>Opens Windows Update for driver updates (the only safe driver path).</summary>
    void OpenWindowsUpdate();

    /// <summary>Checks GitHub releases for a newer BKKleaner version.</summary>
    Task<string?> CheckSelfUpdateAsync(CancellationToken ct = default);
}
