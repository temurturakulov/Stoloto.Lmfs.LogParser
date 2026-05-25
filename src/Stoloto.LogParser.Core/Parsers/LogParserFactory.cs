using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class LogParserFactory
{
    private readonly NLogJsonParser _nlog = new();
    private readonly StolotoJsonParser _stoloto = new();
    private readonly PipeDelimitedParser _pipe = new();

    public (LogEntry? entry, SkippedLine? skipped) TryParse(string line, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(line))
            return (null, null);

        ILogParser? parser = null;
        if (_stoloto.CanParse(line))      parser = _stoloto;
        else if (_nlog.CanParse(line))    parser = _nlog;
        else if (_pipe.CanParse(line))    parser = _pipe;

        if (parser != null)
        {
            var entry = parser.ParseLine(line);
            if (entry != null) return (entry, null);
        }

        return (null, new SkippedLine { LineNumber = lineNumber, RawText = line });
    }
}
