namespace Stoloto.LogParser.Core.Models;

public class SkippedLine
{
    public int LineNumber { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
}
