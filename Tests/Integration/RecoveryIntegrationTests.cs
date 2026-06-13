using BKKleaner.Models;
using BKKleaner.Recovery;
using BKKleaner.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BKKleaner.Tests.Integration;

public class RecoveryIntegrationTests
{
    private static RecoveryService Create(out string dataDir) => Create(out dataDir, out _);

    private static RecoveryService Create(out string dataDir, out BKKleaner.Services.SettingsService settings)
    {
        settings = TestHelpers.CreateSettings(out dataDir);
        var security = new SecurityService(NullLogger<SecurityService>.Instance);
        return new RecoveryService(NullLogger<RecoveryService>.Instance, security, settings);
    }

    [Fact]
    public async Task Config_backup_creates_a_restorable_point()
    {
        var svc = Create(out var dataDir);
        var point = await svc.BackupConfigAsync();

        Assert.NotNull(point);
        Assert.Equal(RecoveryPointKind.ConfigBackup, point!.Kind);
        Assert.True(File.Exists(point.Path));
        Assert.Contains(svc.GetRecoveryPoints(), p => p.Id == point.Id);

        // Restoring copies the file back over settings.json.
        var ok = await svc.RestoreAsync(point);
        Assert.True(ok);
        Assert.True(File.Exists(Path.Combine(dataDir, "settings.json")));
    }

    [Fact]
    public async Task Snapshot_serializes_current_state()
    {
        var svc = Create(out _);
        var point = await svc.CreateSnapshotAsync();
        Assert.NotNull(point);
        Assert.Equal(RecoveryPointKind.Snapshot, point!.Kind);
        var json = await File.ReadAllTextAsync(point.Path!);
        Assert.Contains("Settings", json);
    }

    [Fact]
    public async Task Full_backup_produces_config_and_snapshot_at_minimum()
    {
        var svc = Create(out _, out var settings);
        // Restore points need admin + System Restore enabled — exclude them for the assertion.
        settings.Update(s => s.CreateRestorePointBeforeOptimization = false);

        var created = await svc.CreateFullBackupAsync("integration-test");
        Assert.Contains(created, p => p.Kind == RecoveryPointKind.ConfigBackup);
        Assert.Contains(created, p => p.Kind == RecoveryPointKind.Snapshot);
    }
}
