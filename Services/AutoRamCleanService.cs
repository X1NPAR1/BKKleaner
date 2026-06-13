using BKKleaner.Models;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Services;

public interface IAutoRamCleanService : IDisposable
{
    /// <summary>The only intervals the scheduler accepts, in minutes.</summary>
    static int[] AllowedIntervalsMinutes => [5, 10, 15, 25, 30, 45, 60, 120];

    DateTime? LastRun { get; }
    RamCleanResult? LastResult { get; }
    event EventHandler<RamCleanResult>? AutoCleanCompleted;

    /// <summary>Re-reads the settings and starts/stops/reschedules the timer.</summary>
    void Refresh();
}

/// <summary>Background scheduler that performs a safe RAM clean on a fixed interval.</summary>
public sealed class AutoRamCleanService : IAutoRamCleanService
{
    private readonly ILogger<AutoRamCleanService> _logger;
    private readonly ISettingsService _settings;
    private readonly IRamCleanerService _ramCleaner;
    private readonly INotificationService _notifications;
    private readonly object _gate = new();

    private Timer? _timer;
    private int _activeIntervalMinutes;
    private bool _running;
    private bool _disposed;

    public DateTime? LastRun { get; private set; }
    public RamCleanResult? LastResult { get; private set; }

    public event EventHandler<RamCleanResult>? AutoCleanCompleted;

    public AutoRamCleanService(ILogger<AutoRamCleanService> logger, ISettingsService settings,
        IRamCleanerService ramCleaner, INotificationService notifications)
    {
        _logger = logger;
        _settings = settings;
        _ramCleaner = ramCleaner;
        _notifications = notifications;
        _settings.SettingsChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>Snaps an arbitrary value to the nearest allowed interval.</summary>
    internal static int SnapInterval(int requestedMinutes)
    {
        var allowed = IAutoRamCleanService.AllowedIntervalsMinutes;
        return allowed.OrderBy(a => Math.Abs(a - requestedMinutes)).ThenBy(a => a).First();
    }

    public void Refresh()
    {
        lock (_gate)
        {
            if (_disposed) return;
            var cfg = _settings.Current;

            if (!cfg.AutoRamCleanEnabled)
            {
                if (_timer is not null)
                {
                    _timer.Dispose();
                    _timer = null;
                    _activeIntervalMinutes = 0;
                    _logger.LogInformation("Automatic RAM cleaning disabled");
                }
                return;
            }

            var interval = SnapInterval(cfg.AutoRamCleanIntervalMinutes);
            if (_timer is not null && interval == _activeIntervalMinutes) return;

            _timer?.Dispose();
            var period = TimeSpan.FromMinutes(interval);
            _timer = new Timer(_ => OnTick(), null, period, period);
            _activeIntervalMinutes = interval;
            _logger.LogInformation("Automatic RAM cleaning scheduled every {Minutes} minutes", interval);
        }
    }

    private async void OnTick()
    {
        // Guard against overlapping runs when a clean takes longer than the interval.
        lock (_gate)
        {
            if (_running || _disposed) return;
            _running = true;
        }

        try
        {
            var result = await _ramCleaner.CleanAsync(
                trimWorkingSets: true, clearStandbyList: true, optimizeCache: false)
                .ConfigureAwait(false);
            LastRun = DateTime.Now;
            LastResult = result;
            _logger.LogInformation("Automatic RAM clean: {Freed:0} MB freed, {Count} processes trimmed",
                result.FreedMb, result.ProcessesTrimmed);
            _notifications.Notify(
                Localization.Loc.Instance["ram.auto_title"],
                $"{Localization.Loc.Instance["ram.freed"]}: {result.FreedMb:0} MB",
                NotificationLevel.Info);
            AutoCleanCompleted?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Automatic RAM clean failed");
        }
        finally
        {
            lock (_gate) _running = false;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
        }
    }
}
