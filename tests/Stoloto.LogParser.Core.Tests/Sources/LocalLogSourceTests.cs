using FluentAssertions;
using Stoloto.LogParser.Core.Parsers;
using Stoloto.LogParser.Core.Sources;

namespace Stoloto.LogParser.Core.Tests.Sources;

public class LocalLogSourceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly LocalLogSource _source;

    public LocalLogSourceTests()
    {
        Directory.CreateDirectory(_tempDir);
        _source = new LocalLogSource(new LogParserFactory());
    }

    private string CreateLogFile(string name, string[] lines)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllLines(path, lines);
        return path;
    }

    [Fact]
    public async Task ReadAsync_File_ParsesAllEntries()
    {
        var file = CreateLogFile("test.log", [
            "{ \"datetime\": \"2026-05-13 10:00:00.000\", \"level\": \"Info\", \"logger\": \"Test\", \"message\": \"msg1\" }",
            "{ \"datetime\": \"2026-05-13 10:00:01.000\", \"level\": \"Error\", \"logger\": \"Test\", \"message\": \"msg2\" }",
        ]);

        var results = new List<(Models.LogEntry? entry, Models.SkippedLine? skipped)>();
        await foreach (var r in _source.ReadAsync(file, isFile: true, dateFrom: null, dateTo: null, CancellationToken.None))
            results.Add(r);

        results.Should().HaveCount(2);
        results.All(r => r.entry != null).Should().BeTrue();
    }

    [Fact]
    public async Task ReadAsync_File_CollectsSkippedLines()
    {
        var file = CreateLogFile("test.log", [
            "{ \"datetime\": \"2026-05-13 10:00:00.000\", \"level\": \"Info\", \"logger\": \"T\", \"message\": \"ok\" }",
            "this line cannot be parsed",
        ]);

        var results = new List<(Models.LogEntry? entry, Models.SkippedLine? skipped)>();
        await foreach (var r in _source.ReadAsync(file, isFile: true, dateFrom: null, dateTo: null, CancellationToken.None))
            results.Add(r);

        results.Should().HaveCount(2);
        results[0].entry.Should().NotBeNull();
        results[1].skipped.Should().NotBeNull();
        results[1].skipped!.LineNumber.Should().Be(2);
    }

    [Fact]
    public async Task ReadAsync_Folder_ReadsFilesInDateRange()
    {
        CreateLogFile("Lmfs.Api.2026-05-12.log", [
            "{ \"datetime\": \"2026-05-12 10:00:00.000\", \"level\": \"Info\", \"logger\": \"T\", \"message\": \"day1\" }",
        ]);
        CreateLogFile("Lmfs.Api.2026-05-13.log", [
            "{ \"datetime\": \"2026-05-13 10:00:00.000\", \"level\": \"Info\", \"logger\": \"T\", \"message\": \"day2\" }",
        ]);
        CreateLogFile("Lmfs.Api.2026-05-14.log", [
            "{ \"datetime\": \"2026-05-14 10:00:00.000\", \"level\": \"Info\", \"logger\": \"T\", \"message\": \"day3\" }",
        ]);

        var results = new List<(Models.LogEntry? entry, Models.SkippedLine? skipped)>();
        await foreach (var r in _source.ReadAsync(
            _tempDir, isFile: false,
            dateFrom: new DateTime(2026, 5, 12),
            dateTo: new DateTime(2026, 5, 13),
            CancellationToken.None))
            results.Add(r);

        results.Count(r => r.entry != null).Should().Be(2);
        results.Any(r => r.entry?.Message == "day3").Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_SetsSourceFile()
    {
        var file = CreateLogFile("Lmfs.Api.2026-05-13.log", [
            "{ \"datetime\": \"2026-05-13 10:00:00.000\", \"level\": \"Info\", \"logger\": \"T\", \"message\": \"m\" }",
        ]);

        Models.LogEntry? entry = null;
        await foreach (var (e, _) in _source.ReadAsync(file, isFile: true, null, null, CancellationToken.None))
            if (e != null) entry = e;

        entry!.SourceFile.Should().Be(Path.GetFileName(file));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);
}
