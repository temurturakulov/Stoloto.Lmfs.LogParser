using System.Text.Json;
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class NLogJsonParser : ILogParser
{
    public bool CanParse(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('{')
            && trimmed.Contains("\"datetime\"")
            && !trimmed.Contains("\"LogLevel\"");
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

                switch (prop.Name.ToLowerInvariant())
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
                    case "uid":
                        entry.Uid = val;
                        break;
                    case "category":
                        entry.Category = val;
                        break;
                    case "type":
                        entry.Type = val;
                        break;
                    case "url":
                        entry.Url = val;
                        break;
                    case "body":
                        entry.Body = val;
                        break;
                    case "responsetime":
                        if (decimal.TryParse(val, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var rt))
                            entry.ResponseTime = rt;
                        break;
                    case "httpcode":
                        if (int.TryParse(val, out var code)) entry.HttpCode = code;
                        break;
                    case "details":
                    case "exception":
                        entry.Details = val;
                        break;
                    default:
                        if (!string.IsNullOrEmpty(val))
                            entry.Extra[prop.Name] = val;
                        break;
                }
            }

            return entry.Datetime == default ? null : entry;
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
        "trace"   => "Trace",
        _         => level
    };
}
