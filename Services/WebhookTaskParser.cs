using BKKleaner.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BKKleaner.Services;

/// <summary>Stateless parser/validator for webhook.txt content. Strict whitelist.</summary>
public static class WebhookTaskParser
{
    public static readonly IReadOnlyList<string> Whitelist =
        ["clean_temp", "ram_clean", "optimize_gaming", "switch_theme", "switch_language", "benchmark"];

    public static IReadOnlyList<WebhookTask> Parse(string content, out IReadOnlyList<string> rejectedLines)
    {
        var tasks = new List<WebhookTask>();
        var rejected = new List<string>();
        var lines = content.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim().TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            try
            {
                var obj = JObject.Parse(line);
                var type = obj["type"]?.ToString()?.ToLowerInvariant();
                if (type is null || !Whitelist.Contains(type))
                {
                    rejected.Add($"Line {i + 1}: unknown or forbidden task type '{type ?? "(none)"}'");
                    continue;
                }
                tasks.Add(new WebhookTask
                {
                    Type = type,
                    Value = obj["value"]?.ToString(),
                    LineNumber = i + 1
                });
            }
            catch (JsonException)
            {
                rejected.Add($"Line {i + 1}: invalid JSON");
            }
        }

        rejectedLines = rejected;
        return tasks;
    }
}
