using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Core.Sources;

namespace Stoloto.LogParser.Web.Services;

public class LogQueryService(LocalLogSource source)
{
    public async Task<LogQueryResult> QueryAsync(LogQuery query, CancellationToken ct = default)
    {
        var all = new List<LogEntry>();
        var skipped = new List<SkippedLine>();

        await foreach (var (entry, skip) in source.ReadAsync(
            query.Path, query.IsFile, query.DateFrom, query.DateTo, ct))
        {
            if (entry != null && Matches(entry, query)) all.Add(entry);
            if (skip != null) skipped.Add(skip);
        }

        if (query.SortAsc)
            all.Sort((a, b) => a.Datetime.CompareTo(b.Datetime));
        else
            all.Sort((a, b) => b.Datetime.CompareTo(a.Datetime));

        var total = all.Count;
        var items = all
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return new LogQueryResult
        {
            Items = items,
            Total = total,
            Page = query.Page,
            PageSize = query.PageSize,
            SkippedLines = skipped
        };
    }

    public async Task<LogStatsResult> StatsAsync(string path, bool isFile, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct = default)
    {
        var byLevel    = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byDateHour = new Dictionary<DateTime, Dictionary<string, int>>();

        await foreach (var (entry, _) in source.ReadAsync(path, isFile, dateFrom, dateTo, ct))
        {
            if (entry == null) continue;
            var level = entry.Level ?? "Unknown";
            byLevel[level] = byLevel.GetValueOrDefault(level) + 1;
            var slot = entry.Datetime.Date.AddHours(entry.Datetime.Hour);
            if (!byDateHour.ContainsKey(slot)) byDateHour[slot] = new();
            byDateHour[slot][level] = byDateHour[slot].GetValueOrDefault(level) + 1;
        }

        List<Dictionary<string, object>> hours;
        if (byDateHour.Count == 0)
        {
            hours = Enumerable.Range(0, 24).Select(h =>
                new Dictionary<string, object> { ["hour"] = h, ["label"] = $"{h:D2}:00" }
            ).ToList();
        }
        else
        {
            var minDate = byDateHour.Keys.Min().Date;
            var maxDate = byDateHour.Keys.Max().Date;
            hours = new List<Dictionary<string, object>>();
            for (var date = minDate; date <= maxDate; date = date.AddDays(1))
            {
                for (var h = 0; h < 24; h++)
                {
                    var slot = date.AddHours(h);
                    var row = new Dictionary<string, object>
                    {
                        ["hour"]  = h,
                        ["label"] = $"{slot:dd.MM HH:00}"
                    };
                    if (byDateHour.TryGetValue(slot, out var counts))
                        foreach (var kv in counts) row[kv.Key] = kv.Value;
                    hours.Add(row);
                }
            }
        }

        return new LogStatsResult { ByLevel = byLevel, ByHour = hours };
    }

    public async Task<TraceResult> TraceAsync(string uid, string path, bool isFile, DateTime? date, CancellationToken ct = default)
    {
        var entries = new List<LogEntry>();
        var dateFrom = date?.Date;
        var dateTo   = date?.Date;

        await foreach (var (entry, _) in source.ReadAsync(path, isFile, dateFrom, dateTo, ct))
        {
            if (entry?.Uid == uid) entries.Add(entry);
        }

        entries.Sort((a, b) => a.Datetime.CompareTo(b.Datetime));

        var totalMs = entries.Count >= 2
            ? (decimal)(entries[^1].Datetime - entries[0].Datetime).TotalMilliseconds
            : 0;

        return new TraceResult { Entries = entries, TotalDurationMs = totalMs };
    }

    private static bool Matches(LogEntry e, LogQuery q)
    {
        if (q.Levels.Count > 0 && !q.Levels.Contains(e.Level, StringComparer.OrdinalIgnoreCase))
            return false;
        if (q.Categories.Count > 0 && (e.Category == null || !q.Categories.Contains(e.Category, StringComparer.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrEmpty(q.Type) && !string.Equals(e.Type, q.Type, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(q.Uid) && !string.Equals(e.Uid, q.Uid, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.IsNullOrEmpty(q.UrlContains) && (e.Url == null || !e.Url.Contains(q.UrlContains, StringComparison.OrdinalIgnoreCase)))
            return false;
        if (!string.IsNullOrEmpty(q.Search))
        {
            var s = q.Search;
            if (!(e.Message.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                  (e.Body?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                  (e.Url?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)))
                return false;
        }
        return true;
    }
}

public class LogStatsResult
{
    public Dictionary<string, int> ByLevel { get; set; } = new();
    public List<Dictionary<string, object>> ByHour { get; set; } = new();
}

public class TraceResult
{
    public List<LogEntry> Entries { get; set; } = new();
    public decimal TotalDurationMs { get; set; }
}
