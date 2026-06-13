using System.IO;
using System.Windows;
using BKKleaner.Localization;
using BKKleaner.Models;
using BKKleaner.Optimization;
using BKKleaner.Security;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BKKleaner.Services;

/// <summary>
/// Executes the validated automation tasks found in webhook.txt at startup.
/// Strict whitelist — anything not recognized is rejected and logged, never executed.
/// </summary>
public sealed class WebhookAutomationService : IWebhookAutomationService
{
    private readonly ILogger<WebhookAutomationService> _logger;
    private readonly ISecurityService _security;
    private readonly ISettingsService _settings;
    private readonly ITempCleanerService _tempCleaner;
    private readonly IRamCleanerService _ramCleaner;
    private readonly IProfileService _profiles;
    private readonly IThemeService _themes;
    private readonly ILocalizationService _localization;
    private readonly Benchmark.IBenchmarkService _benchmark;

    public IReadOnlyCollection<string> AllowedTypes => WebhookTaskParser.Whitelist.ToList();

    public WebhookAutomationService(
        ILogger<WebhookAutomationService> logger,
        ISecurityService security,
        ISettingsService settings,
        ITempCleanerService tempCleaner,
        IRamCleanerService ramCleaner,
        IProfileService profiles,
        IThemeService themes,
        ILocalizationService localization,
        Benchmark.IBenchmarkService benchmark)
    {
        _logger = logger;
        _security = security;
        _settings = settings;
        _tempCleaner = tempCleaner;
        _ramCleaner = ramCleaner;
        _profiles = profiles;
        _themes = themes;
        _localization = localization;
        _benchmark = benchmark;
    }

    public IReadOnlyList<WebhookTask> Parse(string content, out IReadOnlyList<string> rejectedLines) =>
        WebhookTaskParser.Parse(content, out rejectedLines);

    public async Task<WebhookExecutionReport> ProcessStartupFileAsync(CancellationToken ct = default)
    {
        var report = new WebhookExecutionReport();
        if (!_settings.Current.WebhookAutomationEnabled) return report;

        var path = Path.Combine(AppContext.BaseDirectory, "webhook.txt");
        if (!File.Exists(path)) return report;

        string content;
        try
        {
            content = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read webhook.txt");
            return report;
        }

        var tasks = Parse(content, out var rejectedLines);
        foreach (var rejection in rejectedLines)
        {
            _logger.LogWarning("webhook.txt rejected: {Reason}", rejection);
            report.Rejected.Add(rejection);
        }

        foreach (var task in tasks)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (await ExecuteAsync(task, ct).ConfigureAwait(false))
                {
                    report.Executed.Add(task.Type);
                    _logger.LogInformation("webhook.txt task executed: {Type}", task.Type);
                }
                else
                {
                    report.Failed.Add(task.Type);
                    _logger.LogWarning("webhook.txt task failed: {Type}", task.Type);
                }
            }
            catch (Exception ex)
            {
                report.Failed.Add(task.Type);
                _logger.LogError(ex, "webhook.txt task crashed: {Type}", task.Type);
            }
        }

        // Rename so the same file is not re-executed on next launch.
        try
        {
            var processed = Path.Combine(AppContext.BaseDirectory,
                $"webhook.processed_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.Move(path, processed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not archive webhook.txt");
        }

        return report;
    }

    private async Task<bool> ExecuteAsync(WebhookTask task, CancellationToken ct)
    {
        switch (task.Type)
        {
            case "clean_temp":
            {
                if (!_security.HasPermission(AppPermission.CleanTemp)) return false;
                var items = await _tempCleaner.ScanAsync(CleanMode.Smart, ct).ConfigureAwait(false);
                var result = await _tempCleaner.CleanAsync(items, CleanMode.Smart, ct).ConfigureAwait(false);
                return result.Errors.Count == 0 || result.ItemsRemoved > 0;
            }
            case "ram_clean":
            {
                if (!_security.HasPermission(AppPermission.CleanRam)) return false;
                var result = await _ramCleaner.CleanAsync(true, true, false, ct).ConfigureAwait(false);
                return result.ProcessesTrimmed > 0 || result.StandbyListCleared;
            }
            case "optimize_gaming":
                if (!_security.HasPermission(AppPermission.ModifyRegistry)) return false;
                return await _profiles.ApplyAsync("balanced", ct).ConfigureAwait(false);
            case "switch_theme":
                return task.Value is not null && await OnUiThreadAsync(() => _themes.ApplyTheme(task.Value)).ConfigureAwait(false);
            case "switch_language":
                return task.Value is not null && _localization.SetLanguage(task.Value);
            case "benchmark":
                await _benchmark.RunAsync("webhook", ct).ConfigureAwait(false);
                return true;
            default:
                return false;
        }
    }

    private static async Task<bool> OnUiThreadAsync(Func<bool> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) return action();
        return await dispatcher.InvokeAsync(action);
    }
}
