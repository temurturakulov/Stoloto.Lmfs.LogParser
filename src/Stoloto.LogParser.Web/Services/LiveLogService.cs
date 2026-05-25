using System.Collections.Concurrent;
using System.Threading.Channels;
using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Core.Sources;

namespace Stoloto.LogParser.Web.Services;

public class LiveSession
{
    public string Path { get; set; } = string.Empty;
    public bool IsFile { get; set; }
    public List<string> Levels { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public long LastOffset { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public Channel<List<LogEntry>> Channel { get; } =
        System.Threading.Channels.Channel.CreateUnbounded<List<LogEntry>>();
}

public class LiveLogService(LocalLogSource source, SettingsService settingsService) : BackgroundService
{
    private readonly ConcurrentDictionary<string, LiveSession> _sessions = new();

    public string AddSession(string path, bool isFile, List<string> levels, List<string> categories)
    {
        var id = Guid.NewGuid().ToString();
        var session = new LiveSession
        {
            Path = path,
            IsFile = isFile,
            Levels = levels,
            Categories = categories,
            CurrentFile = isFile ? path : GetTodayFile(path)
        };
        _sessions[id] = session;
        return id;
    }

    public void RemoveSession(string id) => _sessions.TryRemove(id, out _);

    public LiveSession? GetSession(string id) => _sessions.TryGetValue(id, out var s) ? s : null;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var (id, session) in _sessions)
            {
                try { await PollSessionAsync(session, ct); }
                catch { _sessions.TryRemove(id, out _); }
            }

            var settings = settingsService.Load();
            await Task.Delay(TimeSpan.FromSeconds(settings.LivePollingIntervalSec), ct);
        }
    }

    private async Task PollSessionAsync(LiveSession session, CancellationToken ct)
    {
        var todayFile = session.IsFile ? session.Path : GetTodayFile(session.Path);

        if (todayFile != session.CurrentFile)
        {
            session.CurrentFile = todayFile;
            session.LastOffset = 0;
        }

        if (!File.Exists(session.CurrentFile)) return;

        var newEntries = new List<LogEntry>();
        long newOffset = session.LastOffset;

        await foreach (var (entry, _, offset) in source.TailAsync(session.CurrentFile, session.LastOffset, ct))
        {
            newOffset = offset;
            if (entry == null) continue;
            if (session.Levels.Count > 0 && !session.Levels.Contains(entry.Level, StringComparer.OrdinalIgnoreCase)) continue;
            if (session.Categories.Count > 0 && (entry.Category == null || !session.Categories.Contains(entry.Category, StringComparer.OrdinalIgnoreCase))) continue;
            newEntries.Add(entry);
        }

        session.LastOffset = newOffset;

        if (newEntries.Count > 0)
            await session.Channel.Writer.WriteAsync(newEntries, ct);
    }

    private static string GetTodayFile(string folder)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        return System.IO.Path.Combine(folder, $"Lmfs.Api.{today}.log");
    }
}
