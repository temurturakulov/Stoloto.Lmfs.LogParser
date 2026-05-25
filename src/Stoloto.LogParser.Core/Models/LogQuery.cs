namespace Stoloto.LogParser.Core.Models;

public class LogQuery
{
    public string Path { get; set; } = string.Empty;
    public bool IsFile { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public List<string> Levels { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Type { get; set; }
    public string? Uid { get; set; }
    public string? UrlContains { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
    public bool SortAsc { get; set; } = true;
}
