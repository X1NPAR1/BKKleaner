using BKKleaner.Models;
using BKKleaner.Services;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class LogStoreTests
{
    [Fact]
    public void Add_appends_and_raises_event()
    {
        var store = new LogStore();
        LogEntry? received = null;
        store.EntryAdded += (_, e) => received = e;

        store.Add(new LogEntry { Message = "hello" });

        Assert.Single(store.Entries);
        Assert.Equal("hello", received?.Message);
    }

    [Fact]
    public void Buffer_is_capped()
    {
        var store = new LogStore();
        for (var i = 0; i < 6000; i++)
            store.Add(new LogEntry { Message = $"m{i}" });
        Assert.Equal(5000, store.Entries.Count);
        Assert.Equal("m5999", store.Entries[^1].Message);
    }

    [Fact]
    public void Export_writes_readable_file()
    {
        var store = new LogStore();
        store.Add(new LogEntry { Level = "Warning", Category = "Test", Message = "exported line" });
        var path = Path.Combine(Path.GetTempPath(), $"bkk_log_{Guid.NewGuid():N}.txt");

        store.Export(path);

        var content = File.ReadAllText(path);
        Assert.Contains("exported line", content);
        Assert.Contains("[Warning]", content);
        File.Delete(path);
    }

    [Fact]
    public void Clear_empties_the_buffer()
    {
        var store = new LogStore();
        store.Add(new LogEntry { Message = "x" });
        store.Clear();
        Assert.Empty(store.Entries);
    }
}
