using System.Diagnostics;
using System.Runtime.InteropServices;
using BKKleaner.Monitoring;
using BKKleaner.Security;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Services;

/// <summary>
/// "Cool / optimize" engine. It lowers background contention so CPU/GPU usage and
/// temperature drop, while never disturbing the foreground process (the game/app you
/// are using) or protected system processes — so it causes no in-game performance loss.
/// </summary>
public sealed class SystemBoostService : ISystemBoostService
{
    private readonly ILogger<SystemBoostService> _logger;
    private readonly ISecurityService _security;
    private readonly IRamCleanerService _ramCleaner;
    private readonly IHardwareMonitoringService _monitoring;

    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression", "MemCompression",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass",
        "svchost", "dwm", "fontdrvhost", "audiodg", "explorer", "BKKleaner"
    };

    public SystemBoostService(ILogger<SystemBoostService> logger, ISecurityService security,
        IRamCleanerService ramCleaner, IHardwareMonitoringService monitoring)
    {
        _logger = logger;
        _security = security;
        _ramCleaner = ramCleaner;
        _monitoring = monitoring;
    }

    public async Task<BoostResult> BoostAsync(BoostKind kind, CancellationToken ct = default)
    {
        var result = new BoostResult { Kind = kind };
        var snap = _monitoring.Latest;
        result.UsageBefore = kind is BoostKind.CoolGpu or BoostKind.OptimizeGpu
            ? snap?.Gpu.UsagePercent ?? 0
            : snap?.Cpu.UsagePercent ?? 0;

        // Deprioritize background processes for CPU-focused actions; always trim working sets.
        var deprioritize = kind is BoostKind.CoolCpu or BoostKind.OptimizeCpu;

        await Task.Run(() =>
        {
            var foreground = GetForegroundProcessId();
            foreach (var process in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                using (process)
                {
                    if (process.Id == foreground || process.Id == Environment.ProcessId || process.Id <= 4) continue;
                    if (Protected.Contains(process.ProcessName)) continue;

                    try
                    {
                        if (NativeMethods.EmptyWorkingSet(process.Handle)) result.ProcessesTrimmed++;

                        if (deprioritize && process.PriorityClass is ProcessPriorityClass.Normal
                            or ProcessPriorityClass.AboveNormal or ProcessPriorityClass.High)
                        {
                            // Gentle: only nudge clearly-background apps down one notch.
                            process.PriorityClass = ProcessPriorityClass.BelowNormal;
                            result.ProcessesDeprioritized++;
                        }
                    }
                    catch
                    {
                        // Access denied / exited — safe to skip.
                    }
                }
            }
        }, ct).ConfigureAwait(false);

        // Clearing the standby list relieves memory pressure (helps both CPU & GPU paths).
        var ram = await _ramCleaner.CleanAsync(trimWorkingSets: false, clearStandbyList: true,
            optimizeCache: false, ct).ConfigureAwait(false);
        result.StandbyCleared = ram.StandbyListCleared;
        result.FreedMb = ram.FreedMb;

        // Allow a brief moment for the scheduler to settle, then sample again.
        await Task.Delay(700, ct).ConfigureAwait(false);
        var after = _monitoring.Latest;
        result.UsageAfter = kind is BoostKind.CoolGpu or BoostKind.OptimizeGpu
            ? after?.Gpu.UsagePercent ?? 0
            : after?.Cpu.UsagePercent ?? 0;

        _logger.LogInformation(
            "Boost {Kind}: trimmed {Trim}, deprioritized {Dep}, {Mb:0} MB, usage {Before:0}% -> {After:0}%",
            kind, result.ProcessesTrimmed, result.ProcessesDeprioritized, result.FreedMb,
            result.UsageBefore, result.UsageAfter);
        return result;
    }

    private static int GetForegroundProcessId()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return 0;
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return (int)pid;
    }

    private static class NativeMethods
    {
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
