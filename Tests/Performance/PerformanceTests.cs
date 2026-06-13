using System.Diagnostics;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Performance;

/// <summary>Loose performance guards so regressions in startup time / memory are caught.</summary>
public class PerformanceTests
{
    [Fact]
    public void Localization_loads_all_languages_quickly()
    {
        var sw = Stopwatch.StartNew();
        var svc = new LocalizationService(NullLogger<LocalizationService>.Instance, TestHelpers.LocalizationDir);
        sw.Stop();
        Assert.Equal(5, svc.AvailableLanguages.Count);
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Localization load took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Log_store_handles_high_volume_without_unbounded_memory()
    {
        var store = new LogStore();
        var before = GC.GetTotalMemory(forceFullCollection: true);

        for (var i = 0; i < 50_000; i++)
            store.Add(new LogEntry { Message = $"message {i}", Category = "Perf" });

        var after = GC.GetTotalMemory(forceFullCollection: true);
        Assert.Equal(5000, store.Entries.Count);
        // The capped buffer must stay far below the size of 50k retained entries.
        Assert.True(after - before < 50_000_000, $"Memory grew by {(after - before) / 1024 / 1024} MB");
    }

    [Fact]
    public void Settings_roundtrip_is_fast()
    {
        var svc = TestHelpers.CreateSettings(out _);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 50; i++)
            svc.Update(s => s.MonitoringIntervalMs = 1000 + i);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 5000, $"50 saves took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Webhook_parser_processes_large_files_quickly()
    {
        var content = string.Join('\n',
            Enumerable.Repeat("{\"type\":\"benchmark\"}", 10_000));
        var sw = Stopwatch.StartNew();
        var tasks = WebhookTaskParser.Parse(content, out _);
        sw.Stop();
        Assert.Equal(10_000, tasks.Count);
        Assert.True(sw.ElapsedMilliseconds < 5000, $"Parse took {sw.ElapsedMilliseconds} ms");
    }
}
