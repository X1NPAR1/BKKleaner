using BKKleaner.Models;

namespace BKKleaner.Services;

public interface IUpdateService
{
    /// <summary>True when the Windows Package Manager (winget) is installed and usable.</summary>
    bool IsWingetAvailable { get; }

    /// <summary>Collects available updates from winget plus runtime/OS-managed checks.</summary>
    Task<IReadOnlyList<UpdateItem>> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Upgrades a single package silently; informational rows open Windows Update.</summary>
    Task<bool> UpgradeAsync(UpdateItem item, CancellationToken ct = default);

    /// <summary>Upgrades every installable item in order, reporting the current package name.</summary>
    Task<int> UpgradeAllAsync(IEnumerable<UpdateItem> items, IProgress<string>? progress,
        CancellationToken ct = default);

    /// <summary>Opens Windows Update for driver and OS-component updates (the only safe path).</summary>
    void OpenWindowsUpdate();

    /// <summary>Checks GitHub releases for a newer BKKleaner version.</summary>
    Task<string?> CheckSelfUpdateAsync(CancellationToken ct = default);
}
