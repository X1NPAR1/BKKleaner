using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace BKKleaner.Services;

public enum NotificationLevel { Info, Success, Warning, Error }

/// <summary>A transient in-app toast; auto-dismisses after a few seconds or via its close button.</summary>
public sealed partial class ToastNotification : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Title { get; init; }
    public required string Message { get; init; }
    public NotificationLevel Level { get; init; }
    public DateTime CreatedAt { get; } = DateTime.Now;

    public string Glyph => Level switch
    {
        NotificationLevel.Success => "",
        NotificationLevel.Warning => "",
        NotificationLevel.Error => "",
        _ => ""
    };
}

public interface INotificationService
{
    /// <summary>Currently visible in-app toasts (UI-thread bound).</summary>
    ReadOnlyObservableCollection<ToastNotification> Active { get; }

    /// <summary>
    /// Shows an in-app toast and (optionally) a Windows tray balloon. Both are transient.
    /// Safe to call from any thread.
    /// </summary>
    void Notify(string title, string message, NotificationLevel level = NotificationLevel.Info, bool windows = true);

    void Dismiss(ToastNotification toast);

    /// <summary>Set by the shell window to forward Windows balloon notifications.</summary>
    Action<string, string, NotificationLevel>? WindowsNotifier { get; set; }
}
