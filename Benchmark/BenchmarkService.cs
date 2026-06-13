using System.Diagnostics;
using System.IO;
using System.Text;
using BKKleaner.Models;
using BKKleaner.Monitoring;
using BKKleaner.Services;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Benchmark;

/// <summary>
/// Lightweight in-process micro-benchmarks. Deliberately short (a few seconds)
/// so before/after comparisons around an optimization are practical.
/// </summary>
public sealed class BenchmarkService : IBenchmarkService
{
    private readonly ILogger<BenchmarkService> _logger;
    private readonly IHardwareMonitoringService _monitoring;
    private readonly string _reportDirectory;

    public BenchmarkService(ILogger<BenchmarkService> logger, IHardwareMonitoringService monitoring,
        ISettingsService settings)
    {
        _logger = logger;
        _monitoring = monitoring;
        _reportDirectory = Path.Combine(settings.DataDirectory, "BenchmarkReports");
        Directory.CreateDirectory(_reportDirectory);
    }

    public async Task<BenchmarkResult> RunAsync(string label, CancellationToken ct = default)
    {
        _logger.LogInformation("Benchmark '{Label}' started", label);

        var latency = await MeasureTimerLatencyAsync(ct).ConfigureAwait(false);
        var single = await Task.Run(() => MeasureCpu(1, ct), ct).ConfigureAwait(false);
        var multi = await Task.Run(() => MeasureCpu(Environment.ProcessorCount, ct), ct).ConfigureAwait(false);
        var bandwidth = await Task.Run(() => MeasureMemoryBandwidth(ct), ct).ConfigureAwait(false);

        var snapshot = _monitoring.Latest;
        var cpuLoad = snapshot?.Cpu.UsagePercent ?? 0;
        var ramUsage = snapshot?.Ram.UsagePercent ?? 0;
        var gpuLoad = snapshot?.Gpu.UsagePercent ?? 0;

        var result = new BenchmarkResult
        {
            Label = label,
            CpuLoadPercent = Math.Round(cpuLoad, 1),
            RamUsagePercent = Math.Round(ramUsage, 1),
            LatencyMs = Math.Round(latency, 3),
            CpuSingleThreadScore = Math.Round(single, 0),
            CpuMultiThreadScore = Math.Round(multi, 0),
            MemoryBandwidthMbps = Math.Round(bandwidth, 0),
            FpsEstimate = Math.Round(EstimateFps(single, latency, cpuLoad, gpuLoad), 0)
        };

        _logger.LogInformation(
            "Benchmark '{Label}' done: ST {St}, MT {Mt}, {Bw} MB/s, latency {Lat} ms, FPS est. {Fps}",
            label, result.CpuSingleThreadScore, result.CpuMultiThreadScore,
            result.MemoryBandwidthMbps, result.LatencyMs, result.FpsEstimate);
        return result;
    }

    /// <summary>
    /// Heuristic FPS estimate: scales a reference frame budget by single-thread
    /// throughput and penalizes scheduler latency and current load. An estimate,
    /// not a real render benchmark.
    /// </summary>
    internal static double EstimateFps(double singleThreadScore, double latencyMs, double cpuLoad, double gpuLoad)
    {
        const double referenceScore = 1500; // typical modern desktop core
        var cpuFactor = Math.Clamp(singleThreadScore / referenceScore, 0.2, 2.0);
        var latencyFactor = Math.Clamp(1.0 - (latencyMs - 1.0) * 0.05, 0.5, 1.0);
        var loadFactor = Math.Clamp(1.0 - Math.Max(cpuLoad, gpuLoad) / 100.0 * 0.6, 0.3, 1.0);
        return 144.0 * cpuFactor * latencyFactor * loadFactor;
    }

    private static async Task<double> MeasureTimerLatencyAsync(CancellationToken ct)
    {
        const int samples = 50;
        var total = 0.0;
        var sw = new Stopwatch();
        for (var i = 0; i < samples; i++)
        {
            ct.ThrowIfCancellationRequested();
            sw.Restart();
            await Task.Delay(1, ct).ConfigureAwait(false);
            total += sw.Elapsed.TotalMilliseconds - 1.0;
        }
        return Math.Max(0, total / samples);
    }

    private static double MeasureCpu(int threads, CancellationToken ct)
    {
        const int durationMs = 800;
        long totalOps = 0;
        var workers = new Task<long>[threads];
        for (var t = 0; t < threads; t++)
        {
            workers[t] = Task.Run(() =>
            {
                long ops = 0;
                var x = 1.0001;
                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < durationMs)
                {
                    ct.ThrowIfCancellationRequested();
                    for (var i = 0; i < 10_000; i++)
                        x = Math.Sqrt(x * 1.0001) + 0.0001;
                    ops += 10_000;
                }
                // Prevent the JIT from eliminating the loop.
                return ops + (x > double.MaxValue ? 1 : 0);
            }, ct);
        }
        Task.WaitAll(workers, ct);
        foreach (var w in workers) totalOps += w.Result;
        return totalOps / (double)durationMs; // kOps/sec per ms ≈ score
    }

    private static double MeasureMemoryBandwidth(CancellationToken ct)
    {
        const int size = 64 * 1024 * 1024;
        var source = new byte[size];
        var target = new byte[size];
        new Random(42).NextBytes(source);

        var sw = Stopwatch.StartNew();
        long copied = 0;
        while (sw.ElapsedMilliseconds < 600)
        {
            ct.ThrowIfCancellationRequested();
            Buffer.BlockCopy(source, 0, target, 0, size);
            copied += size;
        }
        return copied / sw.Elapsed.TotalSeconds / 1024.0 / 1024.0;
    }

    public BenchmarkComparison Compare(BenchmarkResult before, BenchmarkResult after) =>
        new() { Before = before, After = after };

    public async Task<string> ExportReportAsync(BenchmarkComparison c, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# BKKleaner Benchmark Comparison Report");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Before ({c.Before.Label}) | After ({c.After.Label}) | Delta |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine($"| FPS estimate | {c.Before.FpsEstimate} | {c.After.FpsEstimate} | {c.FpsDeltaPercent:+0.##;-0.##;0}% |");
        sb.AppendLine($"| CPU load % | {c.Before.CpuLoadPercent} | {c.After.CpuLoadPercent} | {c.CpuLoadDelta:+0.##;-0.##;0} |");
        sb.AppendLine($"| RAM usage % | {c.Before.RamUsagePercent} | {c.After.RamUsagePercent} | {c.RamDelta:+0.##;-0.##;0} |");
        sb.AppendLine($"| Latency ms | {c.Before.LatencyMs} | {c.After.LatencyMs} | {c.LatencyDeltaPercent:+0.##;-0.##;0}% |");
        sb.AppendLine($"| CPU single-thread | {c.Before.CpuSingleThreadScore} | {c.After.CpuSingleThreadScore} | |");
        sb.AppendLine($"| CPU multi-thread | {c.Before.CpuMultiThreadScore} | {c.After.CpuMultiThreadScore} | |");
        sb.AppendLine($"| Memory MB/s | {c.Before.MemoryBandwidthMbps} | {c.After.MemoryBandwidthMbps} | |");

        var path = Path.Combine(_reportDirectory, $"benchmark_{DateTime.Now:yyyyMMdd_HHmmss}.md");
        await File.WriteAllTextAsync(path, sb.ToString(), ct).ConfigureAwait(false);
        _logger.LogInformation("Benchmark report exported to {Path}", path);
        return path;
    }
}
