using BKKleaner.Models;

namespace BKKleaner.Monitoring;

public sealed record ThresholdWarning(string MetricKey, double Value, double Threshold);

public interface IHardwareMonitoringService : IDisposable
{
    HardwareSnapshot? Latest { get; }
    IReadOnlyList<HardwareSnapshot> History { get; }

    event EventHandler<HardwareSnapshot>? SnapshotUpdated;
    event EventHandler<ThresholdWarning>? WarningRaised;

    void Start();
    void Stop();

    /// <summary>Exports the in-memory history as CSV.</summary>
    Task ExportHistoryAsync(string filePath, CancellationToken ct = default);
}
