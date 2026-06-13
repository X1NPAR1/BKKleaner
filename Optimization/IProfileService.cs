using BKKleaner.Models;

namespace BKKleaner.Optimization;

public interface IProfileService
{
    IReadOnlyList<GamingProfile> Profiles { get; }
    GamingProfile? ActiveProfile { get; }

    /// <summary>Lists every change a profile would make (per-action previews).</summary>
    Task<IReadOnlyList<ActionPreview>> PreviewAsync(string profileId);

    /// <summary>Creates a full backup, then applies all actions of the profile.</summary>
    Task<bool> ApplyAsync(string profileId, CancellationToken ct = default);

    /// <summary>Undoes the actions of the active profile (rollback).</summary>
    Task<bool> UndoActiveAsync(CancellationToken ct = default);
}
