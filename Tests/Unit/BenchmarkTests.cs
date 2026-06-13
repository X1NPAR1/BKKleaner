using BKKleaner.Benchmark;
using BKKleaner.Models;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class BenchmarkTests
{
    [Fact]
    public void Comparison_computes_percentage_deltas()
    {
        var before = new BenchmarkResult { FpsEstimate = 100, LatencyMs = 2.0, CpuLoadPercent = 50, RamUsagePercent = 60 };
        var after = new BenchmarkResult { FpsEstimate = 120, LatencyMs = 1.0, CpuLoadPercent = 40, RamUsagePercent = 55 };

        var c = new BenchmarkComparison { Before = before, After = after };

        Assert.Equal(20, c.FpsDeltaPercent);
        Assert.Equal(-50, c.LatencyDeltaPercent);
        Assert.Equal(-10, c.CpuLoadDelta);
        Assert.Equal(-5, c.RamDelta);
    }

    [Fact]
    public void Comparison_handles_zero_baseline()
    {
        var c = new BenchmarkComparison
        {
            Before = new BenchmarkResult { FpsEstimate = 0 },
            After = new BenchmarkResult { FpsEstimate = 100 }
        };
        Assert.Equal(0, c.FpsDeltaPercent);
    }

    [Theory]
    [InlineData(1500, 1.0, 0, 0)]
    [InlineData(3000, 0.5, 10, 5)]
    [InlineData(100, 20, 100, 100)]
    public void Fps_estimate_stays_in_plausible_range(double score, double latency, double cpu, double gpu)
    {
        var fps = BenchmarkService.EstimateFps(score, latency, cpu, gpu);
        // Bounded by the clamps in the formula: 144 * [0.2..2] * [0.5..1] * [0.3..1].
        Assert.InRange(fps, 144 * 0.2 * 0.5 * 0.3, 144 * 2.0);
    }

    [Fact]
    public void Higher_single_thread_score_means_higher_fps_estimate()
    {
        var slow = BenchmarkService.EstimateFps(800, 1.5, 20, 20);
        var fast = BenchmarkService.EstimateFps(2000, 1.5, 20, 20);
        Assert.True(fast > slow);
    }
}
