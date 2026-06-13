using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using BKKleaner.Models;
using Serilog.Core;
using Serilog.Events;

namespace BKKleaner.Services;

public interface ILogStore
{
    ReadOnlyObservableCollection<LogEntry> Entries { get; }
    /// <summary>Lock object for BindingOperations.EnableCollectionSynchronization.</summary>
    object SyncRoot { get; }
    event EventHandler<LogEntry>? EntryAdded;
    void Add(LogEntry entry);
    void Export(string filePath);
    void Clear();
}

/// <summary>In-app log buffer that feeds the Logs page; also receives Serilog events.</summary>
public sealed class LogStore : ILogStore
{
    private const int MaxEntries = 5000;
    private readonly ObservableCollection<LogEntry> _entries = [];
    private readonly object _gate = new();

    public ReadOnlyObservableCollection<LogEntry> Entries { get; }
    public object SyncRoot => _gate;
    public event EventHandler<LogEntry>? EntryAdded;

    public LogStore() => Entries = new ReadOnlyObservableCollection<LogEntry>(_entries);

    public void Add(LogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
            while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
        }
        EntryAdded?.Invoke(this, entry);
    }

    public void Export(string filePath)
    {
        var sb = new StringBuilder();
        lock (_gate)
        {
            foreach (var e in _entries)
                sb.AppendLine($"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{e.Level}] [{e.Category}] {e.Message}");
        }
        File.WriteAllText(filePath, sb.ToString());
    }

    public void Clear()
    {
        lock (_gate) _entries.Clear();
    }
}

/// <summary>Serilog sink that mirrors every log event into the in-app <see cref="LogStore"/>.</summary>
public sealed class LogStoreSink(ILogStore store, IFormatProvider? formatProvider = null) : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        var category = logEvent.Properties.TryGetValue("SourceContext", out var ctx)
            ? ctx.ToString().Trim('"').Split('.').Last()
            : "General";

        store.Add(new LogEntry
        {
            Timestamp = logEvent.Timestamp.LocalDateTime,
            Level = logEvent.Level.ToString(),
            Category = category,
            Message = logEvent.RenderMessage(formatProvider)
        });
    }
}
