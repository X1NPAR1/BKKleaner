using BKKleaner.Models;

namespace BKKleaner.Benchmark;

public interface IBenchmarkService
{
    /// <summary>Runs the full micro-benchmark suite (CPU, memory, latency, FPS estimate).</summary>
    Task<BenchmarkResult> RunAsync(string label, CancellationToken ct = default);

    BenchmarkComparison Compare(BenchmarkResult before, BenchmarkResult after);

    /// <summary>Writes a human-readable comparison report and returns its path.</summary>
    Task<string> ExportReportAsync(BenchmarkComparison comparison, CancellationToken ct = default);
}
