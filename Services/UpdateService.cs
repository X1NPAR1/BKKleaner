using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using BKKleaner.Models;
using BKKleaner.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;

namespace BKKleaner.Services;

public sealed class UpdateService : IUpdateService
{
    private const string RepoApi = "https://api.github.com/repos/X1NPAR1/BKKleaner/releases/latest";

    private readonly ILogger<UpdateService> _logger;
    private readonly ISecurityService _security;
    private readonly HttpClient _http;

    public UpdateService(ILogger<UpdateService> logger, ISecurityService security)
    {
        _logger = logger;
        _security = security;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("BKKleaner");
    }

    public async Task<IReadOnlyList<UpdateItem>> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var items = new List<UpdateItem>();
        items.AddRange(await QueryWingetUpgradesAsync(ct).ConfigureAwait(false));
        items.AddRange(CheckRuntimes());
        _logger.LogInformation("Update check finished: {Count} items", items.Count);
        return items;
    }

    private async Task<List<UpdateItem>> QueryWingetUpgradesAsync(CancellationToken ct)
    {
        var items = new List<UpdateItem>();
        try
        {
            var psi = new ProcessStartInfo("winget",
                "upgrade --accept-source-agreements --disable-interactivity")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            using var process = Process.Start(psi);
            if (process is null) return items;
            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            // Parse fixed-width winget table: Name  Id  Version  Available  Source
            var lines = output.Split('\n');
            var headerIndex = Array.FindIndex(lines, l => l.Contains("Id") && l.Contains("Available"));
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
                if (line.Contains("upgrades available") || string.IsNullOrWhiteSpace(line)) continue;

                var name = line[..Math.Min(idCol, line.Length)].Trim();
                var id = Slice(line, idCol, versionCol).Trim();
                var version = Slice(line, versionCol, availableCol).Trim();
                var available = sourceCol > 0
                    ? Slice(line, availableCol, sourceCol).Trim()
                    : line[availableCol..].Trim();
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(available)) continue;

                items.Add(new UpdateItem
                {
                    Name = string.IsNullOrEmpty(name) ? id : name,
                    Kind = id.Contains("VCRedist", StringComparison.OrdinalIgnoreCase) ? UpdateKind.VcRedist
                         : id.Contains("DotNet", StringComparison.OrdinalIgnoreCase) ? UpdateKind.DotNetRuntime
                         : UpdateKind.InstalledApp,
                    CurrentVersion = version,
                    AvailableVersion = available,
                    Source = id
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "winget query failed (winget may not be installed)");
        }
        return items;
    }

    private static string Slice(string line, int start, int end) =>
        start >= line.Length ? string.Empty : line[start..Math.Min(end, line.Length)];

    private List<UpdateItem> CheckRuntimes()
    {
        var items = new List<UpdateItem>();

        // VC++ 2015-2022 x64 runtime presence check.
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");
            var installed = key?.GetValue("Installed") as int? == 1;
            if (!installed)
            {
                items.Add(new UpdateItem
                {
                    Name = "Microsoft Visual C++ 2015-2022 Redistributable (x64)",
                    Kind = UpdateKind.VcRedist,
                    CurrentVersion = "missing",
                    AvailableVersion = "latest",
                    Source = "Microsoft.VCRedist.2015+.x64"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VC++ runtime check failed");
        }

        // DirectX is serviced through Windows Update on Windows 10/11; report status only.
        items.Add(new UpdateItem
        {
            Name = "DirectX (serviced via Windows Update)",
            Kind = UpdateKind.DirectX,
            CurrentVersion = "12",
            AvailableVersion = "managed by OS",
            Source = "WindowsUpdate"
        });

        return items;
    }

    public async Task<bool> UpgradeAsync(UpdateItem item, CancellationToken ct = default)
    {
        if (!item.IsSafe)
        {
            _logger.LogWarning("Refusing unsafe update: {Name}", item.Name);
            return false;
        }

        var result = await _security.ExecuteSafeAsync(AppPermission.RunUpdates, $"Upgrade:{item.Source}", async () =>
        {
            var psi = new ProcessStartInfo("winget",
                $"upgrade --id \"{item.Source}\" --silent --accept-source-agreements --accept-package-agreements --disable-interactivity")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start winget");
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            // Validation: a non-zero exit code means the upgrade did not complete.
            return process.ExitCode == 0;
        }).ConfigureAwait(false);

        return result.Success && result.Value;
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

            var current = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
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
