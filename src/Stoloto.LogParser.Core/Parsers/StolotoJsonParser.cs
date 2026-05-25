using System.Text.Json;
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class StolotoJsonParser : ILogParser
{
    private static readonly HashSet<string> LowercaseCoreFields = new(StringComparer.Ordinal)
    {
        "datetime", "level", "logger", "message"
    };

    private static readonly HashSet<string> SkipUppercaseFields = new(StringComparer.Ordinal)
    {
        "Date", "LogLevel", "Logger", "Message",
        "ActivityId", "ActivityStartTimeUtc", "ActivityDurationMs", "ThreadId"
    };

    public bool CanParse(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('{') && trimmed.Contains("\"LogLevel\"");
    }

    public LogEntry? ParseLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var entry = new LogEntry();

            foreach (var prop in root.EnumerateObject())
            {
                var val = prop.Value.ValueKind == JsonValueKind.String
                    ? prop.Value.GetString() ?? string.Empty
                    : prop.Value.ToString();

                if (LowercaseCoreFields.Contains(prop.Name))
                {
                    switch (prop.Name)
                    {
                        case "datetime":
                            if (DateTime.TryParse(val, out var dt)) entry.Datetime = dt;
                            break;
                        case "level":
                            entry.Level = Normalize(val);
                            break;
                        case "logger":
                            entry.Logger = val;
                            break;
                        case "message":
                            entry.Message = val;
                            break;
                    }
                    continue;
                }

                if (SkipUppercaseFields.Contains(prop.Name)) continue;

                if (!string.IsNullOrEmpty(val))
                    entry.Extra[prop.Name] = val;
            }

            return entry.Datetime == default ? null : entry;
        }
        catch
        {
            return null;
        }
    }

    private static string Normalize(string level) => level.ToUpperInvariant() switch
    {
        "INFO"    => "Info",
        "WARN"    => "Warn",
        "WARNING" => "Warn",
        "ERROR"   => "Error",
        "DEBUG"   => "Debug",
        _         => level
    };
}
