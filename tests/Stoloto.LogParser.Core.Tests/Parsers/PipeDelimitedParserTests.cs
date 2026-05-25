using FluentAssertions;
using Stoloto.LogParser.Core.Parsers;

namespace Stoloto.LogParser.Core.Tests.Parsers;

public class PipeDelimitedParserTests
{
    private readonly PipeDelimitedParser _parser = new();

    private const string SimpleLine =
        "2026-04-29 12:13:00.2700|Info|Microsoft.EntityFrameworkCore.Database.Command|Executed DbCommand (1ms)|";

    private const string WarnLine =
        "2026-04-29 12:13:03.0313|Warn|Microsoft.AspNetCore.Server.Kestrel|Overriding address(es) 'http://localhost:5039'.|";

    private const string NoTrailingPipeLine =
        "2026-04-29 12:13:00.2700|Info|Logger|Message";

    [Fact]
    public void CanParse_ReturnsTrueForPipeLine()
    {
        _parser.CanParse(SimpleLine).Should().BeTrue();
        _parser.CanParse(WarnLine).Should().BeTrue();
        _parser.CanParse(NoTrailingPipeLine).Should().BeTrue();
    }

    [Fact]
    public void CanParse_ReturnsFalseForJsonLine()
    {
        _parser.CanParse("{ \"datetime\": \"2026-05-13\", \"level\": \"Info\" }").Should().BeFalse();
    }

    [Fact]
    public void ParseLine_ParsesAllPositionalFields()
    {
        var entry = _parser.ParseLine(SimpleLine);

        entry.Should().NotBeNull();
        entry!.Datetime.Should().BeCloseTo(new DateTime(2026, 4, 29, 12, 13, 0, 270), TimeSpan.FromMilliseconds(1));
        entry.Level.Should().Be("Info");
        entry.Logger.Should().Be("Microsoft.EntityFrameworkCore.Database.Command");
        entry.Message.Should().Be("Executed DbCommand (1ms)");
    }

    [Fact]
    public void ParseLine_HandlesTrailingPipe()
    {
        var entry = _parser.ParseLine(SimpleLine);
        entry!.Message.Should().Be("Executed DbCommand (1ms)");
    }

    [Fact]
    public void ParseLine_HandlesNoTrailingPipe()
    {
        var entry = _parser.ParseLine(NoTrailingPipeLine);
        entry.Should().NotBeNull();
        entry!.Message.Should().Be("Message");
    }

    [Fact]
    public void ParseLine_NormalizesWarnLevel()
    {
        var entry = _parser.ParseLine(WarnLine);
        entry!.Level.Should().Be("Warn");
    }

    [Fact]
    public void ParseLine_ReturnsNullForInvalidLine()
    {
        _parser.ParseLine("not a pipe line at all").Should().BeNull();
    }
}
