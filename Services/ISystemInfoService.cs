namespace BKKleaner.Services;

public sealed record SystemInfo(
    string MachineName,
    string OsDescription,
    string CpuName,
    int LogicalCores,
    double TotalRamGb,
    string GpuName);

public sealed record ProcessUsage(string Name, int Id, double MemoryMb, double CpuPercent);

public interface ISystemInfoService
{
    /// <summary>Static machine description, gathered once and cached.</summary>
    SystemInfo GetSystemInfo();

    /// <summary>System uptime since last boot.</summary>
    TimeSpan GetUptime();

    /// <summary>The N processes using the most memory right now.</summary>
    IReadOnlyList<ProcessUsage> GetTopProcessesByMemory(int count);
}
