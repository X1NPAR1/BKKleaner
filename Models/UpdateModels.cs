namespace BKKleaner.Models;

public enum UpdateKind
{
    InstalledApp,
    Driver,
    VcRedist,
    DirectX,
    DotNetRuntime
}

public sealed class UpdateItem
{
    public required string Name { get; init; }
    public required UpdateKind Kind { get; init; }
    public string CurrentVersion { get; init; } = string.Empty;
    public string AvailableVersion { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public bool IsSafe { get; init; } = true;
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Level { get; init; } = "Information";
    public string Category { get; init; } = "General";
    public string Message { get; init; } = string.Empty;
}
