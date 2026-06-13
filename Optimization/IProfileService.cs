using BKKleaner.Models;

namespace BKKleaner.Optimization;

public interface IProfileService
{
    IReadOnlyList<GamingProfile> Profiles { get; }
    GamingProfile? ActiveProfile { get; }

    /// <summary>Every optimization action available to assign to a profile.</summary>
    IReadOnlyList<OptimizationAction> AvailableActions { get; }

    event EventHandler? ProfilesChanged;

    /// <summary>Lists every change a profile would make (per-action previews).</summary>
    Task<IReadOnlyList<ActionPreview>> PreviewAsync(string profileId);

    /// <summary>Creates a full backup, then applies all actions of the profile.</summary>
    Task<bool> ApplyAsync(string profileId, CancellationToken ct = default);

    /// <summary>Undoes the actions of the active profile (rollback).</summary>
    Task<bool> UndoActiveAsync(CancellationToken ct = default);

    /// <summary>Replaces the action set of a profile and persists the customization.</summary>
    void UpdateProfileActions(string profileId, IEnumerable<string> actionIds);

    /// <summary>Restores a built-in profile to its default action set.</summary>
    void ResetProfile(string profileId);

    /// <summary>Creates a new user-defined profile and returns it.</summary>
    GamingProfile CreateCustomProfile(string name, IEnumerable<string> actionIds);

    /// <summary>Deletes a user-created profile (built-in profiles cannot be deleted).</summary>
    bool DeleteProfile(string profileId);
}
