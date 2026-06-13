using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace BKKleaner.Services;

public sealed class SystemInfoService : ISystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;
    private SystemInfo? _cached;

    public SystemInfoService(ILogger<SystemInfoService> logger) => _logger = logger;

    public SystemInfo GetSystemInfo()
    {
        if (_cached is not null) return _cached;

        var cpuName = "Unknown CPU";
        var gpuName = "Unknown GPU";
        double totalRamGb = 0;

        try
        {
            using var cpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var o in cpuSearcher.Get())
            {
                cpuName = o["Name"]?.ToString()?.Trim() ?? cpuName;
                break;
            }

            using var ramSearcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var o in ramSearcher.Get())
            {
                totalRamGb = Convert.ToDouble(o["TotalPhysicalMemory"] ?? 0d) / 1024 / 1024 / 1024;
                break;
            }

            using var gpuSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (var o in gpuSearcher.Get())
            {
                var name = o["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(name)) { gpuName = name; break; }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WMI system info query failed");
        }

        _cached = new SystemInfo(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            cpuName,
            Environment.ProcessorCount,
            Math.Round(totalRamGb, 1),
            gpuName);
        return _cached;
    }

    public TimeSpan GetUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public IReadOnlyList<ProcessUsage> GetTopProcessesByMemory(int count)
    {
        var list = new List<ProcessUsage>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    list.Add(new ProcessUsage(
                        process.ProcessName,
                        process.Id,
                        process.WorkingSet64 / 1024.0 / 1024.0,
                        0));
                }
                catch
                {
                    // Process exited or access denied — skip.
                }
            }
        }
        return list.OrderByDescending(p => p.MemoryMb).Take(count).ToList();
    }
}
