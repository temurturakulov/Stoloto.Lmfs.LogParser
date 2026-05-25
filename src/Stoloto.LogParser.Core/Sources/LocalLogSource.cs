using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Core.Parsers;

namespace Stoloto.LogParser.Core.Sources;

public class LocalLogSource(LogParserFactory factory)
{
    public async IAsyncEnumerable<(LogEntry? entry, SkippedLine? skipped)> ReadAsync(
        string path, bool isFile,
        DateTime? dateFrom, DateTime? dateTo,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var files = isFile
            ? [path]
            : GetFilesInRange(path, dateFrom, dateTo);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            await foreach (var item in ReadFileAsync(file, ct))
                yield return item;
        }
    }

    public async IAsyncEnumerable<(LogEntry? entry, SkippedLine? skipped, long newOffset)> TailAsync(
        string filePath, long lastOffset,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(lastOffset, SeekOrigin.Begin);

        using var reader = new StreamReader(fs);
        int lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            var (entry, skipped) = factory.TryParse(line, lineNumber);
            if (entry != null) entry.SourceFile = Path.GetFileName(filePath);
            if (skipped != null) skipped.SourceFile = Path.GetFileName(filePath);
            yield return (entry, skipped, fs.Position);
        }
    }

    private async IAsyncEnumerable<(LogEntry? entry, SkippedLine? skipped)> ReadFileAsync(
        string filePath,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        int lineNumber = 0;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            lineNumber++;
            var (entry, skipped) = factory.TryParse(line, lineNumber);
            if (entry != null) entry.SourceFile = Path.GetFileName(filePath);
            if (skipped != null) skipped.SourceFile = Path.GetFileName(filePath);
            yield return (entry, skipped);
        }
    }

    private static string[] GetFilesInRange(string folder, DateTime? dateFrom, DateTime? dateTo)
    {
        return Directory.GetFiles(folder, "*.log")
            .Where(f => IsFileInRange(f, dateFrom, dateTo))
            .OrderBy(f => f)
            .ToArray();
    }

    private static bool IsFileInRange(string filePath, DateTime? dateFrom, DateTime? dateTo)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var parts = name.Split('.');
        var datePart = parts.LastOrDefault(p => p.Length == 10 && p.Contains('-'));

        if (datePart == null || !DateTime.TryParse(datePart, out var fileDate))
            return true;

        if (dateFrom.HasValue && fileDate.Date < dateFrom.Value.Date) return false;
        if (dateTo.HasValue && fileDate.Date > dateTo.Value.Date) return false;
        return true;
    }
}
