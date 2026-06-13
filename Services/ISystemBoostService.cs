namespace BKKleaner.Services;

public enum BoostKind { CoolCpu, CoolGpu, OptimizeCpu, OptimizeGpu }

public sealed class BoostResult
{
    public BoostKind Kind { get; init; }
    public double FreedMb { get; set; }
    public int ProcessesTrimmed { get; set; }
    public int ProcessesDeprioritized { get; set; }
    public bool StandbyCleared { get; set; }
    public double UsageBefore { get; set; }
    public double UsageAfter { get; set; }
}

public interface ISystemBoostService
{
    /// <summary>
    /// Safely reduces background CPU/GPU load to lower temperature and free resources,
    /// without touching the foreground app (e.g. a running game) or protected processes.
    /// </summary>
    Task<BoostResult> BoostAsync(BoostKind kind, CancellationToken ct = default);
}
