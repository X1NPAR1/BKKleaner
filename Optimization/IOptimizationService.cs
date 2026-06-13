using BKKleaner.Models;

namespace BKKleaner.Optimization;

public sealed record ActionPreview(string ActionId, IReadOnlyList<string> Changes);

public interface IOptimizationService
{
    IReadOnlyList<OptimizationAction> Actions { get; }

    /// <summary>Describes exactly what an action would change (current → target).</summary>
    Task<ActionPreview> PreviewAsync(string actionId);

    /// <summary>Applies a safe-only action after backing up the previous values.</summary>
    Task<bool> ApplyAsync(string actionId, CancellationToken ct = default);

    /// <summary>Restores the values recorded when the action was applied.</summary>
    Task<bool> UndoAsync(string actionId, CancellationToken ct = default);

    /// <summary>Undoes every applied action (one-click rollback).</summary>
    Task<int> UndoAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync();
    Task<bool> SetStartupEntryEnabledAsync(StartupEntry entry, bool enabled);
}
