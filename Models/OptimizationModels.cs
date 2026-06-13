namespace BKKleaner.Models;

/// <summary>A single reversible optimization action (registry value, powercfg call, ...).</summary>
public sealed class OptimizationAction
{
    public required string Id { get; init; }
    public required string TitleKey { get; init; }
    public required string DescriptionKey { get; init; }
    public OptimizationCategory Category { get; init; }
    public bool IsApplied { get; set; }
    public bool RequiresRestart { get; init; }
}

public enum OptimizationCategory
{
    Power,
    Gaming,
    Background,
    Startup,
    Scheduling,
    Latency
}

public sealed class StartupEntry
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public bool Enabled { get; set; } = true;
}

public sealed class GamingProfile
{
    public required string Id { get; init; }
    public required string NameKey { get; init; }
    public required string DescriptionKey { get; init; }
    public required IReadOnlyList<string> ActionIds { get; init; }
    public bool IsActive { get; set; }
}

public sealed class AppliedActionRecord
{
    public required string ActionId { get; init; }
    public DateTime AppliedAt { get; init; } = DateTime.Now;
    public Dictionary<string, string?> PreviousValues { get; init; } = [];
}
