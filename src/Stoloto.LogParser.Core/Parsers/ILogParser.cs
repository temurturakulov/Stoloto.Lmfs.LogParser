using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public interface ILogParser
{
    bool CanParse(string line);
    LogEntry? ParseLine(string line);
}
