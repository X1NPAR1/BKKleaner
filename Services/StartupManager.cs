using System.Diagnostics;
using Microsoft.Win32;

namespace BKKleaner.Services;

/// <summary>Manages the "start BKKleaner with Windows" entry in the per-user Run key.</summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BKKleaner";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Enables or disables launch at login. Uses the current executable path.</summary>
    public static void Set(bool enabled, bool startMinimized)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null) return;

            if (enabled)
            {
                var exe = Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;
                var command = startMinimized ? $"\"{exe}\" --minimized" : $"\"{exe}\"";
                key.SetValue(ValueName, command, RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-fatal: the toggle simply won't persist.
        }
    }
}
