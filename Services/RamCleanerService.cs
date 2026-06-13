using System.Diagnostics;
using System.Runtime.InteropServices;
using BKKleaner.Models;
using BKKleaner.Security;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Services;

public sealed class RamCleanerService : IRamCleanerService
{
    private readonly ILogger<RamCleanerService> _logger;
    private readonly ISecurityService _security;

    // Processes that must never be touched.
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "Registry", "Memory Compression", "MemCompression",
        "smss", "csrss", "wininit", "winlogon", "services", "lsass",
        "svchost", "dwm", "fontdrvhost", "audiodg"
    };

    public RamCleanerService(ILogger<RamCleanerService> logger, ISecurityService security)
    {
        _logger = logger;
        _security = security;
    }

    public async Task<RamCleanResult> CleanAsync(bool trimWorkingSets, bool clearStandbyList,
        bool optimizeCache, CancellationToken ct = default)
    {
        var result = await _security.ExecuteSafeAsync(AppPermission.CleanRam, "RamClean", () =>
            Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var res = new RamCleanResult();
                var before = GetMemoryStatus();
                res.UsageBeforePercent = before.percent;

                if (trimWorkingSets) res.ProcessesTrimmed = TrimWorkingSets(ct);
                if (clearStandbyList) res.StandbyListCleared = PurgeStandbyList();
                if (optimizeCache) FlushSystemFileCache();

                var after = GetMemoryStatus();
                res.UsageAfterPercent = after.percent;
                res.FreedMb = Math.Max(0, (after.availableMb - before.availableMb));
                res.Duration = sw.Elapsed;
                return res;
            }, ct)).ConfigureAwait(false);

        if (!result.Success || result.Value is null)
        {
            _logger.LogWarning("RAM clean failed: {Error}", result.Error);
            return new RamCleanResult();
        }

        _logger.LogInformation(
            "RAM clean finished: {Freed:0} MB freed, {Trimmed} processes trimmed, standby cleared: {Standby}",
            result.Value.FreedMb, result.Value.ProcessesTrimmed, result.Value.StandbyListCleared);
        return result.Value;
    }

    private int TrimWorkingSets(CancellationToken ct)
    {
        var trimmed = 0;
        var self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            ct.ThrowIfCancellationRequested();
            using (process)
            {
                if (process.Id == self || process.Id <= 4) continue;
                if (ProtectedProcesses.Contains(process.ProcessName)) continue;

                try
                {
                    if (NativeMethods.EmptyWorkingSet(process.Handle)) trimmed++;
                }
                catch
                {
                    // Access denied for protected/system processes — safe to skip.
                }
            }
        }
        return trimmed;
    }

    private bool PurgeStandbyList()
    {
        if (!NativeMethods.EnablePrivilege("SeProfileSingleProcessPrivilege"))
        {
            _logger.LogWarning("Cannot enable SeProfileSingleProcessPrivilege; standby purge skipped");
            return false;
        }

        var command = NativeMethods.MemoryPurgeStandbyList;
        var status = NativeMethods.NtSetSystemInformation(
            NativeMethods.SystemMemoryListInformation, ref command, sizeof(int));
        if (status != 0)
            _logger.LogWarning("NtSetSystemInformation returned 0x{Status:X8}", status);
        return status == 0;
    }

    private void FlushSystemFileCache()
    {
        if (!NativeMethods.EnablePrivilege("SeIncreaseQuotaPrivilege"))
        {
            _logger.LogWarning("Cannot enable SeIncreaseQuotaPrivilege; cache flush skipped");
            return;
        }
        if (!NativeMethods.SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0))
            _logger.LogWarning("SetSystemFileCacheSize failed: {Error}", Marshal.GetLastWin32Error());
    }

    private static (double percent, double availableMb) GetMemoryStatus()
    {
        var status = new NativeMethods.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>() };
        if (!NativeMethods.GlobalMemoryStatusEx(ref status)) return (0, 0);
        return (status.dwMemoryLoad, status.ullAvailPhys / 1024.0 / 1024.0);
    }

    private static class NativeMethods
    {
        public const int SystemMemoryListInformation = 80;
        public const int MemoryPurgeStandbyList = 4;

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("ntdll.dll")]
        public static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetSystemFileCacheSize(IntPtr minimum, IntPtr maximum, int flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAll,
            ref TOKEN_PRIVILEGES newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        public static bool EnablePrivilege(string privilegeName)
        {
            const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
            const uint TOKEN_QUERY = 0x0008;
            const uint SE_PRIVILEGE_ENABLED = 0x0002;

            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out var token))
                return false;
            try
            {
                if (!LookupPrivilegeValue(null, privilegeName, out var luid)) return false;
                var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
                return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero)
                       && Marshal.GetLastWin32Error() == 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
    }
}
