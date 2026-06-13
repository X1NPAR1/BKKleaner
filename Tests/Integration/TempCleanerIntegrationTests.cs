using BKKleaner.Models;
using BKKleaner.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Integration;

public class TempCleanerIntegrationTests
{
    private static (TempCleanerService svc, string tempRoot) Create()
    {
        var settings = TestHelpers.CreateSettings(out _);
        var svc = new TempCleanerService(NullLogger<TempCleanerService>.Instance, settings);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"bkk_it_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        return (svc, tempRoot);
    }

    [Fact]
    public async Task Clean_quarantines_files_and_restore_brings_them_back()
    {
        var (svc, tempRoot) = Create();
        var file = Path.Combine(tempRoot, "junk.tmp");
        await File.WriteAllTextAsync(file, "junk data");

        var items = new[]
        {
            new TempCleanItem { Path = file, Category = TempCategory.UserTemp, SizeBytes = 9, IsDirectory = false }
        };

        var result = await svc.CleanAsync(items, CleanMode.Smart);
        Assert.Equal(1, result.ItemsRemoved);
        Assert.False(File.Exists(file));
        Assert.NotNull(result.QuarantinePath);

        var snapshotId = Path.GetFileName(result.QuarantinePath!);
        var restored = await svc.RestoreAsync(snapshotId);
        Assert.Equal(1, restored);
        Assert.True(File.Exists(file));
        Assert.Equal("junk data", await File.ReadAllTextAsync(file));

        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task Preview_mode_never_deletes()
    {
        var (svc, tempRoot) = Create();
        var file = Path.Combine(tempRoot, "keep.tmp");
        await File.WriteAllTextAsync(file, "data");

        var items = new[]
        {
            new TempCleanItem { Path = file, Category = TempCategory.UserTemp, SizeBytes = 4, IsDirectory = false }
        };

        var result = await svc.CleanAsync(items, CleanMode.Preview);
        Assert.Equal(0, result.ItemsRemoved);
        Assert.True(File.Exists(file));

        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task Unsafe_paths_are_rejected_during_clean()
    {
        var (svc, tempRoot) = Create();
        var items = new[]
        {
            new TempCleanItem { Path = @"C:\Windows\System32\drivers", Category = TempCategory.UserTemp, SizeBytes = 1, IsDirectory = true }
        };

        var result = await svc.CleanAsync(items, CleanMode.Deep);
        Assert.Equal(0, result.ItemsRemoved);
        Assert.Equal(1, result.ItemsSkipped);
        Assert.Single(result.Errors);

        Directory.Delete(tempRoot, true);
    }

    [Fact]
    public async Task Scan_returns_only_safe_paths()
    {
        var (svc, _) = Create();
        var items = await svc.ScanAsync(CleanMode.Smart);
        Assert.All(items, i => Assert.True(TempCleanerService.IsPathSafeToClean(i.Path), i.Path));
    }
}
