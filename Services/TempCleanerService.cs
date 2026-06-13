using System.Diagnostics;
using System.IO;
using BKKleaner.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BKKleaner.Services;

public sealed class TempCleanerService : ITempCleanerService
{
    private readonly ILogger<TempCleanerService> _logger;
    private readonly string _quarantineRoot;

    public TempCleanerService(ILogger<TempCleanerService> logger, ISettingsService settings)
    {
        _logger = logger;
        _quarantineRoot = Path.Combine(settings.DataDirectory, "Quarantine");
        Directory.CreateDirectory(_quarantineRoot);
    }

    // ---- target discovery -------------------------------------------------

    private static IEnumerable<(string path, TempCategory category)> GetTargets(CleanMode mode)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // Smart targets — always safe.
        yield return (Path.GetTempPath(), TempCategory.UserTemp);
        yield return (Path.Combine(windows, "Temp"), TempCategory.WindowsTemp);
        yield return (Path.Combine(local, "CrashDumps"), TempCategory.CrashDumps);
        yield return (Path.Combine(local, @"Microsoft\Windows\WER\ReportQueue"), TempCategory.CrashDumps);

        if (mode == CleanMode.Smart) yield break;

        // Deep targets.
        yield return (Path.Combine(local, "D3DSCache"), TempCategory.ShaderCache);
        yield return (Path.Combine(local, @"NVIDIA\DXCache"), TempCategory.ShaderCache);
        yield return (Path.Combine(local, @"NVIDIA\GLCache"), TempCategory.ShaderCache);
        yield return (Path.Combine(local, @"AMD\DxCache"), TempCategory.ShaderCache);
        yield return (Path.Combine(local, @"Google\Chrome\User Data\Default\Cache\Cache_Data"), TempCategory.BrowserCache);
        yield return (Path.Combine(local, @"Google\Chrome\User Data\Default\Code Cache"), TempCategory.BrowserCache);
        yield return (Path.Combine(local, @"Microsoft\Edge\User Data\Default\Cache\Cache_Data"), TempCategory.BrowserCache);
        yield return (Path.Combine(local, @"Microsoft\Edge\User Data\Default\Code Cache"), TempCategory.BrowserCache);
        yield return (Path.Combine(local, @"Mozilla\Firefox\Profiles"), TempCategory.BrowserCache);
        yield return (Path.Combine(windows, @"Logs\WindowsUpdate"), TempCategory.LogFiles);
        yield return (Path.Combine(local, "Temp"), TempCategory.UserTemp);
    }

    /// <summary>Guard: a path may only be cleaned when it lives under a known cleanable root.</summary>
    public static bool IsPathSafeToClean(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] allowedRoots =
        [
            Path.GetTempPath(),
            Path.Combine(windows, "Temp"),
            Path.Combine(windows, "Logs"),
            local
        ];

        // Never allow cleaning the roots themselves shallow enough to wipe a profile.
        string[] forbidden =
        [
            windows,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.System)
        ];
        if (forbidden.Any(f => string.Equals(full.TrimEnd('\\'), f.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            return false;

        return allowedRoots.Any(root =>
            full.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(full.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
    }

    public Task<IReadOnlyList<TempCleanItem>> ScanAsync(CleanMode mode, CancellationToken ct = default) =>
        Task.Run<IReadOnlyList<TempCleanItem>>(() =>
        {
            var items = new List<TempCleanItem>();
            foreach (var (root, category) in GetTargets(mode == CleanMode.Preview ? CleanMode.Deep : mode))
            {
                ct.ThrowIfCancellationRequested();
                if (!Directory.Exists(root)) continue;

                if (category == TempCategory.BrowserCache && root.Contains(@"Mozilla\Firefox"))
                {
                    // Firefox: each profile has its own cache2 directory.
                    foreach (var profile in SafeEnumerateDirectories(root))
                    {
                        var cache = Path.Combine(profile, "cache2");
                        if (Directory.Exists(cache)) AddEntries(items, cache, category, ct);
                    }
                    continue;
                }

                AddEntries(items, root, category, ct);
            }
            return items;
        }, ct);

    private static void AddEntries(List<TempCleanItem> items, string root, TempCategory category, CancellationToken ct)
    {
        foreach (var entry in SafeEnumerateEntries(root))
        {
            ct.ThrowIfCancellationRequested();
            if (!IsPathSafeToClean(entry)) continue;
            try
            {
                if (Directory.Exists(entry))
                {
                    items.Add(new TempCleanItem
                    {
                        Path = entry,
                        Category = category,
                        IsDirectory = true,
                        SizeBytes = GetDirectorySize(entry)
                    });
                }
                else if (File.Exists(entry))
                {
                    items.Add(new TempCleanItem
                    {
                        Path = entry,
                        Category = category,
                        IsDirectory = false,
                        SizeBytes = new FileInfo(entry).Length
                    });
                }
            }
            catch
            {
                // Inaccessible entry — skip.
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateEntries(string root)
    {
        try { return Directory.EnumerateFileSystemEntries(root); }
        catch { return []; }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try { return Directory.EnumerateDirectories(root); }
        catch { return []; }
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(file).Length; } catch { /* locked */ }
            }
        }
        catch { /* access denied */ }
        return size;
    }

    // ---- cleaning ---------------------------------------------------------

    public Task<CleaningResult> CleanAsync(IEnumerable<TempCleanItem> items, CleanMode mode, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var result = new CleaningResult { Mode = mode };
            if (mode == CleanMode.Preview)
            {
                result.Duration = sw.Elapsed;
                return result;
            }

            var snapshotId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var snapshotDir = Path.Combine(_quarantineRoot, snapshotId);
            Directory.CreateDirectory(snapshotDir);
            var manifest = new Dictionary<string, string>();
            var index = 0;

            foreach (var item in items.Where(i => i.Selected))
            {
                ct.ThrowIfCancellationRequested();
                if (!IsPathSafeToClean(item.Path))
                {
                    result.ItemsSkipped++;
                    result.Errors.Add($"Rejected unsafe path: {item.Path}");
                    continue;
                }

                try
                {
                    var target = Path.Combine(snapshotDir, $"{index++:D6}_{Path.GetFileName(item.Path)}");
                    if (item.IsDirectory)
                        Directory.Move(item.Path, target);
                    else
                        File.Move(item.Path, target);

                    manifest[target] = item.Path;
                    result.ItemsRemoved++;
                    result.BytesFreed += item.SizeBytes;
                }
                catch
                {
                    // Locked by a running process — skipping is the safe behaviour.
                    result.ItemsSkipped++;
                }
            }

            if (manifest.Count > 0)
            {
                File.WriteAllText(Path.Combine(snapshotDir, "manifest.json"),
                    JsonConvert.SerializeObject(manifest, Formatting.Indented));
                result.QuarantinePath = snapshotDir;
            }
            else
            {
                try { Directory.Delete(snapshotDir, true); } catch { /* best effort */ }
            }

            result.Duration = sw.Elapsed;
            _logger.LogInformation(
                "Temp clean ({Mode}): {Removed} removed, {Skipped} skipped, {Mb:0.0} MB freed",
                mode, result.ItemsRemoved, result.ItemsSkipped, result.BytesFreed / 1024.0 / 1024.0);
            return result;
        }, ct);

    // ---- restore ----------------------------------------------------------

    public IReadOnlyList<string> GetQuarantineSnapshots()
    {
        try
        {
            return Directory.EnumerateDirectories(_quarantineRoot)
                .Where(d => File.Exists(Path.Combine(d, "manifest.json")))
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Select(n => n!)
                .OrderDescending()
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public Task<int> RestoreAsync(string snapshotId, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var snapshotDir = Path.Combine(_quarantineRoot, snapshotId);
            var manifestPath = Path.Combine(snapshotDir, "manifest.json");
            if (!File.Exists(manifestPath)) return 0;

            var manifest = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(manifestPath)) ?? [];
            var restored = 0;
            foreach (var (quarantined, original) in manifest)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var parent = Path.GetDirectoryName(original);
                    if (parent is not null) Directory.CreateDirectory(parent);
                    if (Directory.Exists(quarantined))
                        Directory.Move(quarantined, original);
                    else if (File.Exists(quarantined))
                        File.Move(quarantined, original);
                    restored++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not restore {Path}", original);
                }
            }
            _logger.LogInformation("Restored {Count} items from snapshot {Snapshot}", restored, snapshotId);
            return restored;
        }, ct);

    public int PurgeQuarantine(TimeSpan olderThan)
    {
        var purged = 0;
        var cutoff = DateTime.Now - olderThan;
        foreach (var dir in SafeEnumerateDirectories(_quarantineRoot))
        {
            try
            {
                if (Directory.GetCreationTime(dir) < cutoff)
                {
                    Directory.Delete(dir, true);
                    purged++;
                }
            }
            catch { /* best effort */ }
        }
        if (purged > 0) _logger.LogInformation("Purged {Count} quarantine snapshots", purged);
        return purged;
    }
}
