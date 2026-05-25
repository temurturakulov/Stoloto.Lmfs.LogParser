using FluentAssertions;
using Stoloto.LogParser.Core.Parsers;

namespace Stoloto.LogParser.Core.Tests.Parsers;

public class NLogJsonParserTests
{
    private readonly NLogJsonParser _parser = new();

    private const string SimpleLine =
        "{ \"datetime\": \"2026-05-13 09:59:52.0210\", \"level\": \"Info\", \"logger\": \"Lmfs.Api.Program\", \"message\": \"Сервис запускается\" }";

    private const string OperationLine =
        "{ \"datetime\": \"2026-05-13 09:59:59.3130\", \"level\": \"Info\", \"logger\": \"OperationLogger\", \"message\": \"operation\", \"uid\": \"cae313b9-a222-48b2-8787-0f98ba2b91dc\", \"category\": \"db\", \"type\": \"request\", \"body\": \"Попытка проверить таблицу\", \"url\": \"ValidateSchemaAsync\" }";

    private const string ExtraFieldsLine =
        "{ \"datetime\": \"2026-05-13 10:00:03.9950\", \"level\": \"Info\", \"logger\": \"Microsoft.EF\", \"message\": \"Executed DbCommand\", \"elapsed\": \"33\", \"commandTimeout\": 30, \"EventId\": 20101 }";

    [Fact]
    public void CanParse_ReturnsTrueForNLogJsonLine()
    {
        _parser.CanParse(SimpleLine).Should().BeTrue();
        _parser.CanParse(OperationLine).Should().BeTrue();
    }

    [Fact]
    public void CanParse_ReturnsFalseForPipeLine()
    {
        _parser.CanParse("2026-04-29 12:13:00|Info|Logger|Message|").Should().BeFalse();
    }

    [Fact]
    public void CanParse_ReturnsFalseForStolotoLine()
    {
        _parser.CanParse("{ \"Date\": \"2026-05-12\", \"LogLevel\": \"INFO\", \"datetime\": \"2026-05-12\" }").Should().BeFalse();
    }

    [Fact]
    public void ParseLine_ParsesSimpleFields()
    {
        var entry = _parser.ParseLine(SimpleLine);

        entry.Should().NotBeNull();
        entry!.Datetime.Should().Be(new DateTime(2026, 5, 13, 9, 59, 52, 21));
        entry.Level.Should().Be("Info");
        entry.Logger.Should().Be("Lmfs.Api.Program");
        entry.Message.Should().Be("Сервис запускается");
    }

    [Fact]
    public void ParseLine_ParsesOperationFields()
    {
        var entry = _parser.ParseLine(OperationLine);

        entry.Should().NotBeNull();
        entry!.Uid.Should().Be("cae313b9-a222-48b2-8787-0f98ba2b91dc");
        entry.Category.Should().Be("db");
        entry.Type.Should().Be("request");
        entry.Body.Should().Be("Попытка проверить таблицу");
        entry.Url.Should().Be("ValidateSchemaAsync");
    }

    [Fact]
    public void ParseLine_PutsUnknownFieldsInExtra()
    {
        var entry = _parser.ParseLine(ExtraFieldsLine);

        entry.Should().NotBeNull();
        entry!.Extra.Should().ContainKey("elapsed").WhoseValue.Should().Be("33");
        entry.Extra.Should().ContainKey("commandTimeout");
        entry.Extra.Should().ContainKey("EventId");
    }

    [Fact]
    public void ParseLine_ReturnsNullForNonMatchingLine()
    {
        _parser.ParseLine("not json at all").Should().BeNull();
    }
}
