using FluentAssertions;
using Stoloto.LogParser.Core.Parsers;

namespace Stoloto.LogParser.Core.Tests.Parsers;

public class LogParserFactoryTests
{
    private readonly LogParserFactory _factory = new();

    [Fact]
    public void TryParse_NLogLine_ReturnsEntry()
    {
        var line = "{ \"datetime\": \"2026-05-13 10:00:00.000\", \"level\": \"Info\", \"logger\": \"Test\", \"message\": \"msg\" }";
        var (entry, skipped) = _factory.TryParse(line, 1);

        entry.Should().NotBeNull();
        skipped.Should().BeNull();
        entry!.Level.Should().Be("Info");
    }

    [Fact]
    public void TryParse_StolotoLine_ReturnsEntry()
    {
        var line = "{ \"Date\": \"2026-05-12\", \"LogLevel\": \"INFO\", \"datetime\": \"2026-05-12 10:00:00\", \"level\": \"Info\", \"logger\": \"L\", \"message\": \"m\" }";
        var (entry, skipped) = _factory.TryParse(line, 2);

        entry.Should().NotBeNull();
        skipped.Should().BeNull();
    }

    [Fact]
    public void TryParse_PipeLine_ReturnsEntry()
    {
        var line = "2026-04-29 12:13:00.2700|Info|Logger|Message|";
        var (entry, skipped) = _factory.TryParse(line, 3);

        entry.Should().NotBeNull();
        skipped.Should().BeNull();
    }

    [Fact]
    public void TryParse_UnknownLine_ReturnsSkipped()
    {
        var line = "this is some garbage line that cannot be parsed";
        var (entry, skipped) = _factory.TryParse(line, 42);

        entry.Should().BeNull();
        skipped.Should().NotBeNull();
        skipped!.LineNumber.Should().Be(42);
        skipped.RawText.Should().Be(line);
    }

    [Fact]
    public void TryParse_EmptyLine_ReturnsNull()
    {
        var (entry, skipped) = _factory.TryParse("", 1);
        entry.Should().BeNull();
        skipped.Should().BeNull();
    }

    [Fact]
    public void TryParse_WhitespaceLine_ReturnsNull()
    {
        var (entry, skipped) = _factory.TryParse("   ", 1);
        entry.Should().BeNull();
        skipped.Should().BeNull();
    }
}
