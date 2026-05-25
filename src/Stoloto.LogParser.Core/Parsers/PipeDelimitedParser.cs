using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class PipeDelimitedParser : ILogParser
{
    public bool CanParse(string line)
    {
        return !line.TrimStart().StartsWith('{') && line.Contains('|');
    }

    public LogEntry? ParseLine(string line)
    {
        try
        {
            var parts = line.Split('|');
            if (parts.Length < 4) return null;

            if (!DateTime.TryParseExact(
                    parts[0].Trim(),
                    ["yyyy-MM-dd HH:mm:ss.ffff", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dt))
                return null;

            return new LogEntry
            {
                Datetime = dt,
                Level    = Normalize(parts[1].Trim()),
                Logger   = parts[2].Trim(),
                Message  = parts[3].Trim()
            };
        }
        catch
        {
            return null;
        }
    }

    private static string Normalize(string level) => level.ToLowerInvariant() switch
    {
        "info"    => "Info",
        "warn"    => "Warn",
        "warning" => "Warn",
        "error"   => "Error",
        "debug"   => "Debug",
        _         => level
    };
}
