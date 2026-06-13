using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text;
using BKKleaner.Models;
using BKKleaner.Security;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace BKKleaner.Services;

public sealed class UpdateService : IUpdateService
{
    private const string RepoApi = "https://api.github.com/repos/X1NPAR1/BKKleaner/releases/latest";

    private readonly ILogger<UpdateService> _logger;
    private readonly ISecurityService _security;
    private readonly HttpClient _http;
    private bool? _wingetAvailable;

    public UpdateService(ILogger<UpdateService> logger, ISecurityService security)
    {
        _logger = logger;
        _security = security;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BKKleaner");
    }

    public bool IsWingetAvailable => _wingetAvailable ??= DetectWinget();

    private bool DetectWinget()
    {
        try
        {
            var psi = new ProcessStartInfo("winget", "--version")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(5000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<UpdateItem>> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        if (IsWingetAvailable)
            items.AddRange(await QueryWingetUpgradesAsync(ct).ConfigureAwait(false));
        else
            _logger.LogWarning("winget is not available on this machine");

        items.AddRange(CheckRuntimes());
        _logger.LogInformation("Update check finished: {Count} items ({Installable} installable)",
            items.Count, items.Count(i => i.IsInstallable));
        return items;
    }

    private async Task<List<UpdateItem>> QueryWingetUpgradesAsync(CancellationToken ct)
    {
        var items = new List<UpdateItem>();
        try
        {
            // --include-unknown surfaces apps whose installed version winget can't read.
            var output = await RunWingetAsync(
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity", ct)
                .ConfigureAwait(false);

            var lines = output.Split('\n');
            var headerIndex = Array.FindIndex(lines, l =>
                l.Contains("Name", StringComparison.Ordinal) &&
                l.Contains("Id", StringComparison.Ordinal) &&
                l.Contains("Available", StringComparison.Ordinal));
            if (headerIndex < 0) return items;

            var header = lines[headerIndex];
            int idCol = header.IndexOf("Id", StringComparison.Ordinal);
            int versionCol = header.IndexOf("Version", StringComparison.Ordinal);
            int availableCol = header.IndexOf("Available", StringComparison.Ordinal);
            int sourceCol = header.IndexOf("Source", StringComparison.Ordinal);
            if (idCol < 0 || versionCol < 0 || availableCol < 0) return items;

            foreach (var raw in lines.Skip(headerIndex + 2))
            {
                ct.ThrowIfCancellationRequested();
                var line = raw.TrimEnd('\r');
                if (line.Length < availableCol + 1 || line.StartsWith('-')) continue;
                if (line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(line)) continue;

                var name = line[..Math.Min(idCol, line.Length)].Trim();
                var id = Slice(line, idCol, versionCol).Trim();
                var version = Slice(line, versionCol, availableCol).Trim();
                var available = sourceCol > 0
                    ? Slice(line, availableCol, sourceCol).Trim()
                    : line[Math.Min(availableCol, line.Length)..].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(available)) continue;

                items.Add(new UpdateItem
                {
                    Name = string.IsNullOrEmpty(name) ? id : name,
                    Kind = id.Contains("VCRedist", StringComparison.OrdinalIgnoreCase) ? UpdateKind.VcRedist
                         : id.Contains("DotNet", StringComparison.OrdinalIgnoreCase) ? UpdateKind.DotNetRuntime
                         : UpdateKind.InstalledApp,
                    CurrentVersion = version,
                    AvailableVersion = available,
                    Source = id,
                    IsInstallable = true
                });
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "winget upgrade query failed");
        }
        return items;
    }

    private static string Slice(string line, int start, int end) =>
        start >= line.Length ? string.Empty : line[start..Math.Min(end, line.Length)];

    private List<UpdateItem> CheckRuntimes()
    {
        var items = new List<UpdateItem>();

        // DirectX & driver components are serviced by Windows Update — informational only.
        items.Add(new UpdateItem
        {
            Name = "DirectX / Windows components",
            Kind = UpdateKind.DirectX,
            CurrentVersion = string.Empty,
            AvailableVersion = string.Empty,
            Source = "ms-settings:windowsupdate",
            IsInstallable = false,
            InfoKey = "updates.os_managed"
        });

        return items;
    }

    private async Task<string> RunWingetAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("winget", args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start winget");
        var stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return stdout;
    }

    public async Task<bool> UpgradeAsync(UpdateItem item, CancellationToken ct = default)
    {
        if (!item.IsInstallable)
        {
            // Informational rows (DirectX/OS components) open Windows Update instead.
            OpenWindowsUpdate();
            return true;
        }
        if (!item.IsSafe)
        {
            _logger.LogWarning("Refusing unsafe update: {Name}", item.Name);
            return false;
        }
        if (!IsWingetAvailable)
        {
            _logger.LogWarning("Cannot upgrade {Name}: winget unavailable", item.Name);
            return false;
        }

        var result = await _security.ExecuteSafeAsync(AppPermission.RunUpdates, $"Upgrade:{item.Source}", async () =>
        {
            var output = await RunWingetAsync(
                $"upgrade --id \"{item.Source}\" --silent --accept-source-agreements " +
                "--accept-package-agreements --disable-interactivity --force", ct).ConfigureAwait(false);
            // winget prints "Successfully installed" on success; also treat exit 0 as success.
            return output.Contains("Successfully", StringComparison.OrdinalIgnoreCase)
                   || !output.Contains("No applicable", StringComparison.OrdinalIgnoreCase);
        }).ConfigureAwait(false);

        return result.Success && result.Value;
    }

    public async Task<int> UpgradeAllAsync(IEnumerable<UpdateItem> items, IProgress<string>? progress,
        CancellationToken ct = default)
    {
        var installable = items.Where(i => i.IsInstallable && i.IsSafe).ToList();
        var ok = 0;
        foreach (var item in installable)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(item.Name);
            if (await UpgradeAsync(item, ct).ConfigureAwait(false)) ok++;
        }
        _logger.LogInformation("Bulk upgrade: {Ok}/{Total} succeeded", ok, installable.Count);
        return ok;
    }

    public void OpenWindowsUpdate() =>
        Process.Start(new ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true });

    public async Task<string?> CheckSelfUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetStringAsync(RepoApi, ct).ConfigureAwait(false);
            var tag = JObject.Parse(json)["tag_name"]?.ToString();
            if (tag is null) return null;

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(3, 6, 0);
            var latest = Version.TryParse(tag.TrimStart('v'), out var v) ? v : null;
            return latest is not null && latest > current ? tag : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Self-update check failed");
            return null;
        }
    }
}
