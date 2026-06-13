using System.Diagnostics;
using System.IO;
using System.Management;
using BKKleaner.Models;
using BKKleaner.Security;
using BKKleaner.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BKKleaner.Recovery;

public sealed class RecoveryService : IRecoveryService
{
    private readonly ILogger<RecoveryService> _logger;
    private readonly ISecurityService _security;
    private readonly ISettingsService _settings;
    private readonly string _backupRoot;
    private readonly string _manifestPath;
    private readonly List<RecoveryPoint> _points;
    private readonly object _gate = new();

    public RecoveryService(ILogger<RecoveryService> logger, ISecurityService security, ISettingsService settings)
    {
        _logger = logger;
        _security = security;
        _settings = settings;
        _backupRoot = Path.Combine(settings.DataDirectory, "Backups");
        Directory.CreateDirectory(_backupRoot);
        _manifestPath = Path.Combine(_backupRoot, "recovery-points.json");
        _points = LoadManifest();
    }

    public IReadOnlyList<RecoveryPoint> GetRecoveryPoints()
    {
        lock (_gate) return _points.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<RecoveryPoint?> CreateRestorePointAsync(string description, CancellationToken ct = default)
    {
        var result = await _security.ExecuteSafeAsync(AppPermission.CreateRestorePoint, "CreateRestorePoint", () =>
            Task.Run(() =>
            {
                var scope = new ManagementScope(@"\\localhost\root\default");
                var path = new ManagementPath("SystemRestore");
                using var restore = new ManagementClass(scope, path, new ObjectGetOptions());
                var parameters = restore.GetMethodParameters("CreateRestorePoint");
                parameters["Description"] = description;
                parameters["RestorePointType"] = 12; // MODIFY_SETTINGS
                parameters["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
                var outParams = restore.InvokeMethod("CreateRestorePoint", parameters, null);
                return Convert.ToInt32(outParams["ReturnValue"]);
            }, ct)).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogWarning("Restore point creation failed: {Error}", result.Error);
            return null;
        }
        if (result.Value != 0)
        {
            // 1058 = System Restore disabled; 0x422 etc. Frequency limit also lands here.
            _logger.LogWarning("CreateRestorePoint returned {Code} (System Restore disabled or rate-limited)", result.Value);
            return null;
        }

        var point = NewPoint(RecoveryPointKind.SystemRestorePoint, description, null);
        _logger.LogInformation("System restore point created: {Description}", description);
        return point;
    }

    public async Task<RecoveryPoint?> BackupRegistryKeyAsync(string keyPath, CancellationToken ct = default)
    {
        var safeName = keyPath.Replace('\\', '_').Replace(':', '_');
        var file = Path.Combine(_backupRoot, $"reg_{DateTime.Now:yyyyMMdd_HHmmssfff}_{safeName}.reg");

        var result = await _security.ExecuteSafeAsync(AppPermission.ModifyRegistry, "BackupRegistry", async () =>
        {
            var psi = new ProcessStartInfo("reg.exe", $"export \"{keyPath}\" \"{file}\" /y")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start reg.exe");
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            return process.ExitCode == 0 && File.Exists(file);
        }).ConfigureAwait(false);

        if (!result.Success || !result.Value)
        {
            // Key may simply not exist yet — that is fine, nothing to back up.
            _logger.LogInformation("Registry export skipped for {Key} (key may not exist)", keyPath);
            return null;
        }

        return NewPoint(RecoveryPointKind.RegistryBackup, $"Registry: {keyPath}", file);
    }

    public Task<RecoveryPoint?> BackupConfigAsync(CancellationToken ct = default) =>
        Task.Run(() =>
        {
            try
            {
                var source = Path.Combine(_settings.DataDirectory, "settings.json");
                if (!File.Exists(source)) _settings.Save();
                var target = Path.Combine(_backupRoot, $"config_{DateTime.Now:yyyyMMdd_HHmmssfff}.json");
                File.Copy(source, target, overwrite: true);
                return NewPoint(RecoveryPointKind.ConfigBackup, "Application configuration", target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Config backup failed");
                return null;
            }
        }, ct);

    public Task<RecoveryPoint?> CreateSnapshotAsync(CancellationToken ct = default) =>
        Task.Run(() =>
        {
            try
            {
                var snapshot = new
                {
                    CreatedAt = DateTime.Now,
                    Settings = _settings.Current,
                    Machine = Environment.MachineName,
                    Os = Environment.OSVersion.VersionString
                };
                var target = Path.Combine(_backupRoot, $"snapshot_{DateTime.Now:yyyyMMdd_HHmmssfff}.json");
                File.WriteAllText(target, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
                return NewPoint(RecoveryPointKind.Snapshot, "State snapshot", target);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Snapshot failed");
                return null;
            }
        }, ct);

    public async Task<IReadOnlyList<RecoveryPoint>> CreateFullBackupAsync(string reason, CancellationToken ct = default)
    {
        var created = new List<RecoveryPoint>();

        if (_settings.Current.CreateRestorePointBeforeOptimization)
        {
            var rp = await CreateRestorePointAsync($"BKKleaner: {reason}", ct).ConfigureAwait(false);
            if (rp is not null) created.Add(rp);
        }
        var config = await BackupConfigAsync(ct).ConfigureAwait(false);
        if (config is not null) created.Add(config);
        var snapshot = await CreateSnapshotAsync(ct).ConfigureAwait(false);
        if (snapshot is not null) created.Add(snapshot);

        _logger.LogInformation("Full backup before '{Reason}': {Count} recovery points", reason, created.Count);
        return created;
    }

    public async Task<bool> RestoreAsync(RecoveryPoint point, CancellationToken ct = default)
    {
        switch (point.Kind)
        {
            case RecoveryPointKind.RegistryBackup when point.Path is not null && File.Exists(point.Path):
            {
                var result = await _security.ExecuteSafeAsync(AppPermission.ModifyRegistry, "RestoreRegistry", async () =>
                {
                    var psi = new ProcessStartInfo("reg.exe", $"import \"{point.Path}\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardError = true
                    };
                    using var process = Process.Start(psi)
                        ?? throw new InvalidOperationException("Failed to start reg.exe");
                    await process.WaitForExitAsync(ct).ConfigureAwait(false);
                    return process.ExitCode == 0;
                }).ConfigureAwait(false);
                return result.Success && result.Value;
            }
            case RecoveryPointKind.ConfigBackup when point.Path is not null && File.Exists(point.Path):
            {
                var target = Path.Combine(_settings.DataDirectory, "settings.json");
                File.Copy(point.Path, target, overwrite: true);
                _logger.LogInformation("Configuration restored from {Path}", point.Path);
                return true;
            }
            case RecoveryPointKind.SystemRestorePoint:
                // System restore points are restored through Windows (rstrui).
                Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true });
                return true;
            default:
                _logger.LogWarning("Recovery point {Id} cannot be restored automatically", point.Id);
                return false;
        }
    }

    private RecoveryPoint NewPoint(RecoveryPointKind kind, string description, string? path)
    {
        var point = new RecoveryPoint
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Description = description,
            Path = path
        };
        lock (_gate)
        {
            _points.Add(point);
            File.WriteAllText(_manifestPath, JsonConvert.SerializeObject(_points, Formatting.Indented));
        }
        return point;
    }

    private List<RecoveryPoint> LoadManifest()
    {
        try
        {
            if (File.Exists(_manifestPath))
                return JsonConvert.DeserializeObject<List<RecoveryPoint>>(File.ReadAllText(_manifestPath)) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recovery manifest");
        }
        return [];
    }
}
