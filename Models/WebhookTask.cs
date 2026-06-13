namespace BKKleaner.Models;

/// <summary>One validated automation task parsed from webhook.txt (one JSON object per line).</summary>
public sealed class WebhookTask
{
    public required string Type { get; init; }
    public string? Value { get; init; }
    public int LineNumber { get; init; }
}

public sealed class WebhookExecutionReport
{
    public List<string> Executed { get; } = [];
    public List<string> Rejected { get; } = [];
    public List<string> Failed { get; } = [];
}
