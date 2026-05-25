using FluentAssertions;
using Stoloto.LogParser.Core.Parsers;

namespace Stoloto.LogParser.Core.Tests.Parsers;

public class StolotoJsonParserTests
{
    private readonly StolotoJsonParser _parser = new();

    private const string RequestLine =
        "{ \"Date\": \"2026-05-12 13:47:21.5812Z\", \"LogLevel\": \"INFO\", \"Logger\": \"Stoloto.Framework.Logger.ExternalHttpRequestsLogger\", \"Message\": \"Starting external request\", \"datetime\": \"2026-05-12 16:47:21.5812\", \"level\": \"Info\", \"logger\": \"Stoloto.Framework.Logger.ExternalHttpRequestsLogger\", \"message\": \"Starting external request\", \"HttpRequestUrl\": \"https://almfs.rmmot.ru:12001/login\", \"HttpRequestMethod\": \"POST\", \"HttpRequestBody\": \"{\\\"login\\\":\\\"user\\\"}\" }";

    private const string ResponseLine =
        "{ \"Date\": \"2026-05-12 13:47:22.1329Z\", \"LogLevel\": \"INFO\", \"Logger\": \"Stoloto.Framework.Logger.ExternalHttpRequestsLogger\", \"Message\": \"Finished external request\", \"datetime\": \"2026-05-12 16:47:22.1329\", \"level\": \"Info\", \"logger\": \"Stoloto.Framework.Logger.ExternalHttpRequestsLogger\", \"message\": \"Finished external request\", \"HttpRequestUrl\": \"https://almfs.rmmot.ru:12001/login\", \"HttpRequestMethod\": \"POST\", \"HttpRequstDurationInMs\": 470, \"HttpResponseStatus\": 200 }";

    [Fact]
    public void CanParse_ReturnsTrueForStolotoLine()
    {
        _parser.CanParse(RequestLine).Should().BeTrue();
        _parser.CanParse(ResponseLine).Should().BeTrue();
    }

    [Fact]
    public void CanParse_ReturnsFalseForNLogLine()
    {
        _parser.CanParse("{ \"datetime\": \"2026-05-13\", \"level\": \"Info\", \"message\": \"test\" }").Should().BeFalse();
    }

    [Fact]
    public void ParseLine_UsesLowercaseFieldsForCoreValues()
    {
        var entry = _parser.ParseLine(RequestLine);

        entry.Should().NotBeNull();
        entry!.Datetime.Should().BeCloseTo(new DateTime(2026, 5, 12, 16, 47, 21, 581), TimeSpan.FromMilliseconds(1));
        entry.Level.Should().Be("Info");
        entry.Logger.Should().Be("Stoloto.Framework.Logger.ExternalHttpRequestsLogger");
        entry.Message.Should().Be("Starting external request");
    }

    [Fact]
    public void ParseLine_NormalizesUppercaseLevelToTitleCase()
    {
        var entry = _parser.ParseLine(RequestLine);
        entry!.Level.Should().Be("Info");
    }

    [Fact]
    public void ParseLine_PutsHttpFieldsInExtra()
    {
        var entry = _parser.ParseLine(RequestLine);

        entry.Should().NotBeNull();
        entry!.Extra.Should().ContainKey("HttpRequestUrl");
        entry.Extra.Should().ContainKey("HttpRequestMethod");
        entry.Extra.Should().ContainKey("HttpRequestBody");
    }

    [Fact]
    public void ParseLine_ParsesResponseStatus()
    {
        var entry = _parser.ParseLine(ResponseLine);

        entry.Should().NotBeNull();
        entry!.Extra.Should().ContainKey("HttpResponseStatus").WhoseValue.Should().Be("200");
    }
}
