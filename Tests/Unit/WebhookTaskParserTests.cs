using BKKleaner.Services;
using Xunit;

namespace BKKleaner.Tests.Unit;

public class WebhookTaskParserTests
{
    [Fact]
    public void Parses_whitelisted_tasks()
    {
        const string content = """
            {"type":"clean_temp"}
            {"type":"ram_clean"}
            {"type":"optimize_gaming"}
            {"type":"switch_theme","value":"dark"}
            """;

        var tasks = WebhookTaskParser.Parse(content, out var rejected);

        Assert.Equal(4, tasks.Count);
        Assert.Empty(rejected);
        Assert.Equal("dark", tasks[3].Value);
    }

    [Theory]
    [InlineData("{\"type\":\"format_disk\"}")]
    [InlineData("{\"type\":\"run_command\",\"value\":\"cmd /c del *\"}")]
    [InlineData("{\"type\":\"\"}")]
    [InlineData("{\"value\":\"no type\"}")]
    public void Rejects_dangerous_or_unknown_tasks(string line)
    {
        var tasks = WebhookTaskParser.Parse(line, out var rejected);
        Assert.Empty(tasks);
        Assert.Single(rejected);
    }

    [Fact]
    public void Rejects_invalid_json_with_line_number()
    {
        var tasks = WebhookTaskParser.Parse("not json at all", out var rejected);
        Assert.Empty(tasks);
        Assert.Contains("Line 1", rejected[0]);
    }

    [Fact]
    public void Skips_comments_and_blank_lines()
    {
        const string content = """
            # comment

            {"type":"benchmark"}
            """;
        var tasks = WebhookTaskParser.Parse(content, out var rejected);
        Assert.Single(tasks);
        Assert.Empty(rejected);
        Assert.Equal(3, tasks[0].LineNumber);
    }

    [Fact]
    public void Whitelist_contains_exactly_the_documented_tasks()
    {
        Assert.Equal(
            ["clean_temp", "ram_clean", "optimize_gaming", "switch_theme", "switch_language", "benchmark"],
            WebhookTaskParser.Whitelist);
    }
}
