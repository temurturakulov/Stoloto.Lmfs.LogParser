namespace Stoloto.LogParser.Core.Models;

public class LogEntry
{
    public DateTime Datetime { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Logger { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Uid { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Body { get; set; }
    public decimal? ResponseTime { get; set; }
    public int? HttpCode { get; set; }
    public string? Details { get; set; }
    public Dictionary<string, string> Extra { get; set; } = new();
    public string SourceFile { get; set; } = string.Empty;
}
