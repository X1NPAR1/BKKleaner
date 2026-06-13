using BKKleaner.Models;

namespace BKKleaner.Services;

public interface IWebhookAutomationService
{
    /// <summary>Whitelisted task types that webhook.txt may request.</summary>
    IReadOnlyCollection<string> AllowedTypes { get; }

    /// <summary>Parses and validates webhook.txt content (one JSON object per line).</summary>
    IReadOnlyList<WebhookTask> Parse(string content, out IReadOnlyList<string> rejectedLines);

    /// <summary>Reads webhook.txt next to the executable, validates and executes safe tasks.</summary>
    Task<WebhookExecutionReport> ProcessStartupFileAsync(CancellationToken ct = default);
}
