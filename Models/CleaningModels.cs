namespace BKKleaner.Models;

public enum CleanMode
{
    Preview,
    Smart,
    Deep
}

public enum TempCategory
{
    WindowsTemp,
    UserTemp,
    BrowserCache,
    ShaderCache,
    LogFiles,
    CrashDumps
}

public sealed class TempCleanItem
{
    public required string Path { get; init; }
    public required TempCategory Category { get; init; }
    public long SizeBytes { get; init; }
    public bool IsDirectory { get; init; }
    public bool Selected { get; set; } = true;
}

public sealed class CleaningResult
{
    public CleanMode Mode { get; init; }
    public int ItemsRemoved { get; set; }
    public int ItemsSkipped { get; set; }
    public long BytesFreed { get; set; }
    public string? QuarantinePath { get; set; }
    public List<string> Errors { get; } = [];
    public TimeSpan Duration { get; set; }
}

public sealed class RamCleanResult
{
    public double FreedMb { get; set; }
    public int ProcessesTrimmed { get; set; }
    public bool StandbyListCleared { get; set; }
    public double UsageBeforePercent { get; set; }
    public double UsageAfterPercent { get; set; }
    public TimeSpan Duration { get; set; }
}
