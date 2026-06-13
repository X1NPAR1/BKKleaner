namespace BKKleaner.Models;

public sealed class BenchmarkResult
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string Label { get; init; } = string.Empty;
    public double CpuLoadPercent { get; init; }
    public double RamUsagePercent { get; init; }
    public double LatencyMs { get; init; }
    public double FpsEstimate { get; init; }
    public double CpuSingleThreadScore { get; init; }
    public double CpuMultiThreadScore { get; init; }
    public double MemoryBandwidthMbps { get; init; }
}

public sealed class BenchmarkComparison
{
    public required BenchmarkResult Before { get; init; }
    public required BenchmarkResult After { get; init; }

    public double FpsDeltaPercent => Delta(Before.FpsEstimate, After.FpsEstimate);
    public double CpuLoadDelta => After.CpuLoadPercent - Before.CpuLoadPercent;
    public double RamDelta => After.RamUsagePercent - Before.RamUsagePercent;
    public double LatencyDeltaPercent => Delta(Before.LatencyMs, After.LatencyMs);

    private static double Delta(double before, double after) =>
        before <= 0 ? 0 : Math.Round((after - before) / before * 100.0, 2);
}
