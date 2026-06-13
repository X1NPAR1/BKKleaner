using System.Diagnostics;
using System.IO;
using BKKleaner.Models;
using BKKleaner.Recovery;
using BKKleaner.Security;
using BKKleaner.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace BKKleaner.Optimization;

/// <summary>
/// Safe-only Windows/gaming optimizations. Every action backs up the previous
/// registry values before changing anything and can be undone individually.
/// </summary>
public sealed class OptimizationService : IOptimizationService
{
    private readonly ILogger<OptimizationService> _logger;
    private readonly ISecurityService _security;
    private readonly IRecoveryService _recovery;
    private readonly string _undoStorePath;
    private readonly Dictionary<string, AppliedActionRecord> _undoStore;
    private readonly List<OptimizationAction> _actions;

    private sealed record RegistryChange(string KeyPath, string ValueName, object TargetValue, RegistryValueKind Kind);

    private static readonly Dictionary<string, RegistryChange[]> ActionChanges = new()
    {
        ["game_mode"] =
        [
            new(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AllowAutoGameMode", 1, RegistryValueKind.DWord),
            new(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord)
        ],
        ["background_apps"] =
        [
            new(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications",
                "GlobalUserDisabled", 1, RegistryValueKind.DWord)
        ],
        ["scheduling_gaming"] =
        [
            new(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\PriorityControl",
                "Win32PrioritySeparation", 38, RegistryValueKind.DWord)
        ],
        ["latency_responsiveness"] =
        [
            new(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "SystemResponsiveness", 10, RegistryValueKind.DWord),
            new(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord)
        ],
        ["gpu_scheduling"] =
        [
            new(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                "HwSchMode", 2, RegistryValueKind.DWord)
        ],
        ["cpu_priority_games"] =
        [
            new(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "GPU Priority", 8, RegistryValueKind.DWord),
            new(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "Priority", 6, RegistryValueKind.DWord),
            new(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                "Scheduling Category", "High", RegistryValueKind.String)
        ]
    };

    private const string PowerPlanActionId = "power_high_performance";
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public IReadOnlyList<OptimizationAction> Actions => _actions;

    public OptimizationService(ILogger<OptimizationService> logger, ISecurityService security,
        IRecoveryService recovery, ISettingsService settings)
    {
        _logger = logger;
        _security = security;
        _recovery = recovery;
        _undoStorePath = Path.Combine(settings.DataDirectory, "applied-actions.json");
        _undoStore = LoadUndoStore();

        _actions =
        [
            new() { Id = PowerPlanActionId, TitleKey = "opt.power.title", DescriptionKey = "opt.power.desc", Category = OptimizationCategory.Power },
            new() { Id = "game_mode", TitleKey = "opt.gamemode.title", DescriptionKey = "opt.gamemode.desc", Category = OptimizationCategory.Gaming },
            new() { Id = "background_apps", TitleKey = "opt.background.title", DescriptionKey = "opt.background.desc", Category = OptimizationCategory.Background },
            new() { Id = "scheduling_gaming", TitleKey = "opt.scheduling.title", DescriptionKey = "opt.scheduling.desc", Category = OptimizationCategory.Scheduling },
            new() { Id = "latency_responsiveness", TitleKey = "opt.latency.title", DescriptionKey = "opt.latency.desc", Category = OptimizationCategory.Latency },
            new() { Id = "gpu_scheduling", TitleKey = "opt.gpusched.title", DescriptionKey = "opt.gpusched.desc", Category = OptimizationCategory.Gaming, RequiresRestart = true },
            new() { Id = "cpu_priority_games", TitleKey = "opt.cpupriority.title", DescriptionKey = "opt.cpupriority.desc", Category = OptimizationCategory.Gaming }
        ];

        foreach (var action in _actions)
            action.IsApplied = _undoStore.ContainsKey(action.Id);
    }

    // ---- preview ------------------------------------------------------------

    public Task<ActionPreview> PreviewAsync(string actionId) => Task.Run(() =>
    {
        var changes = new List<string>();
        if (actionId == PowerPlanActionId)
        {
            changes.Add($"Active power plan → High performance ({HighPerformanceGuid})");
        }
        else if (ActionChanges.TryGetValue(actionId, out var regChanges))
        {
            foreach (var c in regChanges)
            {
                var current = Registry.GetValue(c.KeyPath, c.ValueName, null);
                changes.Add($"{c.KeyPath}\\{c.ValueName}: {FormatValue(current)} → {FormatValue(c.TargetValue)}");
            }
        }
        return new ActionPreview(actionId, changes);
    });

    private static string FormatValue(object? value) => value switch
    {
        null => "(not set)",
        int i => $"0x{i:X} ({(uint)i})",
        _ => value.ToString() ?? "(null)"
    };

    // ---- apply / undo --------------------------------------------------------

    public async Task<bool> ApplyAsync(string actionId, CancellationToken ct = default)
    {
        var action = _actions.FirstOrDefault(a => a.Id == actionId);
        if (action is null)
        {
            _logger.LogWarning("Unknown optimization action: {Id}", actionId);
            return false;
        }
        if (action.IsApplied) return true;

        var result = await _security.ExecuteSafeAsync(AppPermission.ModifyRegistry, $"Apply:{actionId}", async () =>
        {
            var record = new AppliedActionRecord { ActionId = actionId };

            if (actionId == PowerPlanActionId)
            {
                var previous = await RunPowercfgAsync("/getactivescheme", ct).ConfigureAwait(false);
                record.PreviousValues["activeScheme"] = ExtractGuid(previous);
                await RunPowercfgAsync($"/setactive {HighPerformanceGuid}", ct).ConfigureAwait(false);
            }
            else if (ActionChanges.TryGetValue(actionId, out var regChanges))
            {
                // Registry backup (.reg export) before any modification.
                foreach (var key in regChanges.Select(c => c.KeyPath).Distinct())
                    await _recovery.BackupRegistryKeyAsync(key, ct).ConfigureAwait(false);

                foreach (var c in regChanges)
                {
                    var current = Registry.GetValue(c.KeyPath, c.ValueName, null);
                    record.PreviousValues[$"{c.KeyPath}|{c.ValueName}"] =
                        current is null ? null : JsonConvert.SerializeObject(new { v = current, k = c.Kind.ToString() });
                    Registry.SetValue(c.KeyPath, c.ValueName, c.TargetValue, c.Kind);
                }
            }
            else
            {
                throw new InvalidOperationException($"Action {actionId} has no change set");
            }

            _undoStore[actionId] = record;
            SaveUndoStore();
            action.IsApplied = true;
            return true;
        }).ConfigureAwait(false);

        return result.Success && result.Value;
    }

    public async Task<bool> UndoAsync(string actionId, CancellationToken ct = default)
    {
        if (!_undoStore.TryGetValue(actionId, out var record)) return false;

        var result = await _security.ExecuteSafeAsync(AppPermission.ModifyRegistry, $"Undo:{actionId}", async () =>
        {
            if (actionId == PowerPlanActionId)
            {
                if (record.PreviousValues.TryGetValue("activeScheme", out var guid) && !string.IsNullOrEmpty(guid))
                    await RunPowercfgAsync($"/setactive {guid}", ct).ConfigureAwait(false);
            }
            else
            {
                foreach (var (compound, serialized) in record.PreviousValues)
                {
                    var sep = compound.LastIndexOf('|');
                    if (sep < 0) continue;
                    var keyPath = compound[..sep];
                    var valueName = compound[(sep + 1)..];

                    if (serialized is null)
                    {
                        DeleteRegistryValue(keyPath, valueName);
                    }
                    else
                    {
                        var stored = JsonConvert.DeserializeAnonymousType(serialized, new { v = (object?)null, k = "" });
                        if (stored?.v is not null && Enum.TryParse<RegistryValueKind>(stored.k, out var kind))
                        {
                            var value = kind == RegistryValueKind.DWord ? Convert.ToInt32(stored.v) : stored.v;
                            Registry.SetValue(keyPath, valueName, value, kind);
                        }
                    }
                }
            }

            _undoStore.Remove(actionId);
            SaveUndoStore();
            var action = _actions.FirstOrDefault(a => a.Id == actionId);
            if (action is not null) action.IsApplied = false;
            return true;
        }).ConfigureAwait(false);

        return result.Success && result.Value;
    }

    public async Task<int> UndoAllAsync(CancellationToken ct = default)
    {
        var count = 0;
        foreach (var id in _undoStore.Keys.ToList())
        {
            if (await UndoAsync(id, ct).ConfigureAwait(false)) count++;
        }
        return count;
    }

    private static void DeleteRegistryValue(string keyPath, string valueName)
    {
        var (root, subKey) = SplitKeyPath(keyPath);
        using var key = root.OpenSubKey(subKey, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    private static (RegistryKey root, string subKey) SplitKeyPath(string keyPath)
    {
        var idx = keyPath.IndexOf('\\');
        var rootName = keyPath[..idx];
        var sub = keyPath[(idx + 1)..];
        var root = rootName switch
        {
            "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            "HKEY_CURRENT_USER" => Registry.CurrentUser,
            _ => throw new ArgumentException($"Unsupported hive: {rootName}")
        };
        return (root, sub);
    }

    private static async Task<string> RunPowercfgAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powercfg", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start powercfg");
        var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return output;
    }

    private static string ExtractGuid(string powercfgOutput)
    {
        var parts = powercfgOutput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(p => Guid.TryParse(p, out _)) ?? string.Empty;
    }

    // ---- startup entries -----------------------------------------------------

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync() =>
        Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            var entries = new List<StartupEntry>();
            CollectStartup(Registry.CurrentUser, "HKCU", entries);
            CollectStartup(Registry.LocalMachine, "HKLM", entries);
            return entries;
        });

    private static void CollectStartup(RegistryKey hive, string label, List<StartupEntry> entries)
    {
        using var run = hive.OpenSubKey(RunKey);
        if (run is null) return;
        using var approved = hive.OpenSubKey(ApprovedKey);

        foreach (var name in run.GetValueNames())
        {
            var enabled = true;
            if (approved?.GetValue(name) is byte[] state && state.Length > 0)
                enabled = (state[0] & 0x01) == 0 || state[0] == 0x02; // 0x02 = enabled, 0x03 = disabled

            entries.Add(new StartupEntry
            {
                Name = name,
                Command = run.GetValue(name)?.ToString() ?? string.Empty,
                Location = label,
                Enabled = enabled
            });
        }
    }

    public Task<bool> SetStartupEntryEnabledAsync(StartupEntry entry, bool enabled) =>
        Task.Run(() =>
        {
            try
            {
                var hive = entry.Location == "HKCU" ? Registry.CurrentUser : Registry.LocalMachine;
                using var approved = hive.CreateSubKey(ApprovedKey);
                var state = new byte[12];
                state[0] = enabled ? (byte)0x02 : (byte)0x03;
                approved.SetValue(entry.Name, state, RegistryValueKind.Binary);
                entry.Enabled = enabled;
                _logger.LogInformation("Startup entry {Name} set to {State}", entry.Name, enabled ? "enabled" : "disabled");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle startup entry {Name}", entry.Name);
                return false;
            }
        });

    // ---- undo store persistence ----------------------------------------------

    private Dictionary<string, AppliedActionRecord> LoadUndoStore()
    {
        try
        {
            if (File.Exists(_undoStorePath))
                return JsonConvert.DeserializeObject<Dictionary<string, AppliedActionRecord>>(
                    File.ReadAllText(_undoStorePath)) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load undo store");
        }
        return [];
    }

    private void SaveUndoStore() =>
        File.WriteAllText(_undoStorePath, JsonConvert.SerializeObject(_undoStore, Formatting.Indented));
}
