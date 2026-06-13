using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Services;

public sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan AutoDismiss = TimeSpan.FromSeconds(5);

    private readonly ILogger<NotificationService> _logger;
    private readonly ObservableCollection<ToastNotification> _active = [];

    public ReadOnlyObservableCollection<ToastNotification> Active { get; }
    public Action<string, string, NotificationLevel>? WindowsNotifier { get; set; }

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
        Active = new ReadOnlyObservableCollection<ToastNotification>(_active);
    }

    public void Notify(string title, string message, NotificationLevel level = NotificationLevel.Info, bool windows = true)
    {
        _logger.LogInformation("Notification [{Level}] {Title}: {Message}", level, title, message);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // No UI (tests) — only the Windows hook, if any.
            WindowsNotifier?.Invoke(title, message, level);
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            var toast = new ToastNotification { Title = title, Message = message, Level = level };
            _active.Insert(0, toast);
            while (_active.Count > 4) _active.RemoveAt(_active.Count - 1);

            var timer = new DispatcherTimer { Interval = AutoDismiss };
            timer.Tick += (s, _) =>
            {
                ((DispatcherTimer)s!).Stop();
                Dismiss(toast);
            };
            timer.Start();

            if (windows) WindowsNotifier?.Invoke(title, message, level);
        });
    }

    public void Dismiss(ToastNotification toast)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        dispatcher.BeginInvoke(() => _active.Remove(toast));
    }
}
