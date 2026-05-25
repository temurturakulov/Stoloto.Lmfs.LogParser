# MVP 1 Local Reader — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать локальный веб-просмотрщик логов Lmfs.Api — читает файлы с диска, парсит три формата из одного файла, отображает в фильтруемом интерактивном UI.

**Architecture:** ASP.NET Core 8 раздаёт и API и статику. Core-библиотека отвечает за парсинг и фильтрацию. Три формата логов определяются и парсятся построчно из одного дневного файла. Live-режим реализован через SSE polling.

**Tech Stack:** .NET 8, ASP.NET Core 8 minimal API, System.Text.Json, xUnit, FluentAssertions, Bootstrap 5.3 CDN, vanilla JS ES modules, SSE.

---

## Структура файлов

```
C:\Frame\Stoloto.LogParser\
├── Stoloto.LogParser.sln
├── src\
│   ├── Stoloto.LogParser.Core\
│   │   ├── Stoloto.LogParser.Core.csproj
│   │   ├── Models\
│   │   │   ├── LogEntry.cs
│   │   │   ├── LogQuery.cs
│   │   │   ├── LogQueryResult.cs
│   │   │   ├── SkippedLine.cs
│   │   │   └── UserSettings.cs
│   │   ├── Parsers\
│   │   │   ├── ILogParser.cs
│   │   │   ├── LogParserFactory.cs
│   │   │   ├── NLogJsonParser.cs
│   │   │   ├── PipeDelimitedParser.cs
│   │   │   └── StolotoJsonParser.cs
│   │   └── Sources\
│   │       └── LocalLogSource.cs
│   └── Stoloto.LogParser.Web\
│       ├── Stoloto.LogParser.Web.csproj
│       ├── Program.cs
│       ├── Controllers\
│       │   ├── LogsController.cs
│       │   └── SettingsController.cs
│       ├── Services\
│       │   ├── LiveLogService.cs
│       │   ├── LogQueryService.cs
│       │   └── SettingsService.cs
│       └── wwwroot\
│           ├── index.html
│           ├── settings.html
│           ├── trace.html
│           └── js\
│               ├── api.js
│               ├── app.js
│               ├── settings.js
│               └── trace.js
└── tests\
    └── Stoloto.LogParser.Core.Tests\
        ├── Stoloto.LogParser.Core.Tests.csproj
        ├── Parsers\
        │   ├── LogParserFactoryTests.cs
        │   ├── NLogJsonParserTests.cs
        │   ├── PipeDelimitedParserTests.cs
        │   └── StolotoJsonParserTests.cs
        └── Sources\
            └── LocalLogSourceTests.cs
```

---

## Task 1: Scaffold решения

**Files:**
- Create: `Stoloto.LogParser.sln`
- Create: `src/Stoloto.LogParser.Core/Stoloto.LogParser.Core.csproj`
- Create: `src/Stoloto.LogParser.Web/Stoloto.LogParser.Web.csproj`
- Create: `tests/Stoloto.LogParser.Core.Tests/Stoloto.LogParser.Core.Tests.csproj`

- [ ] **Step 1: Создать solution и проекты**

```powershell
cd C:\Frame\Stoloto.LogParser
dotnet new sln -n Stoloto.LogParser
dotnet new classlib -n Stoloto.LogParser.Core -o src/Stoloto.LogParser.Core --framework net8.0
dotnet new web -n Stoloto.LogParser.Web -o src/Stoloto.LogParser.Web --framework net8.0
dotnet new xunit -n Stoloto.LogParser.Core.Tests -o tests/Stoloto.LogParser.Core.Tests --framework net8.0
```

- [ ] **Step 2: Добавить проекты в solution**

```powershell
dotnet sln add src/Stoloto.LogParser.Core/Stoloto.LogParser.Core.csproj
dotnet sln add src/Stoloto.LogParser.Web/Stoloto.LogParser.Web.csproj
dotnet sln add tests/Stoloto.LogParser.Core.Tests/Stoloto.LogParser.Core.Tests.csproj
```

- [ ] **Step 3: Добавить ссылки между проектами**

```powershell
dotnet add src/Stoloto.LogParser.Web/Stoloto.LogParser.Web.csproj reference src/Stoloto.LogParser.Core/Stoloto.LogParser.Core.csproj
dotnet add tests/Stoloto.LogParser.Core.Tests/Stoloto.LogParser.Core.Tests.csproj reference src/Stoloto.LogParser.Core/Stoloto.LogParser.Core.csproj
```

- [ ] **Step 4: Добавить NuGet-пакеты в тесты**

```powershell
dotnet add tests/Stoloto.LogParser.Core.Tests/Stoloto.LogParser.Core.Tests.csproj package FluentAssertions --version 6.12.0
```

- [ ] **Step 5: Удалить Class1.cs из Core**

```powershell
Remove-Item src/Stoloto.LogParser.Core/Class1.cs
```

- [ ] **Step 6: Проверить сборку**

```powershell
dotnet build
```

Ожидаемый вывод: `Build succeeded.`

- [ ] **Step 7: Commit**

```powershell
git init
git add .
git commit -m "feat: scaffold solution with Core, Web, Tests projects"
```

---

## Task 2: Core модели

**Files:**
- Create: `src/Stoloto.LogParser.Core/Models/LogEntry.cs`
- Create: `src/Stoloto.LogParser.Core/Models/LogQuery.cs`
- Create: `src/Stoloto.LogParser.Core/Models/LogQueryResult.cs`
- Create: `src/Stoloto.LogParser.Core/Models/SkippedLine.cs`
- Create: `src/Stoloto.LogParser.Core/Models/UserSettings.cs`

- [ ] **Step 1: Создать LogEntry.cs**

```csharp
// src/Stoloto.LogParser.Core/Models/LogEntry.cs
namespace Stoloto.LogParser.Core.Models;

public class LogEntry
{
    public DateTime Datetime { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Logger { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Uid { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Url { get; set; }
    public string? Body { get; set; }
    public decimal? ResponseTime { get; set; }
    public int? HttpCode { get; set; }
    public string? Details { get; set; }
    public Dictionary<string, string> Extra { get; set; } = new();
    public string SourceFile { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Создать SkippedLine.cs**

```csharp
// src/Stoloto.LogParser.Core/Models/SkippedLine.cs
namespace Stoloto.LogParser.Core.Models;

public class SkippedLine
{
    public int LineNumber { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Создать LogQuery.cs**

```csharp
// src/Stoloto.LogParser.Core/Models/LogQuery.cs
namespace Stoloto.LogParser.Core.Models;

public class LogQuery
{
    public string Path { get; set; } = string.Empty;
    public bool IsFile { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public List<string> Levels { get; set; } = new();
    public List<string> Categories { get; set; } = new();
    public string? Type { get; set; }
    public string? Uid { get; set; }
    public string? UrlContains { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
```

- [ ] **Step 4: Создать LogQueryResult.cs**

```csharp
// src/Stoloto.LogParser.Core/Models/LogQueryResult.cs
namespace Stoloto.LogParser.Core.Models;

public class LogQueryResult
{
    public List<LogEntry> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<SkippedLine> SkippedLines { get; set; } = new();
}
```

- [ ] **Step 5: Создать UserSettings.cs**

```csharp
// src/Stoloto.LogParser.Core/Models/UserSettings.cs
namespace Stoloto.LogParser.Core.Models;

public class UserSettings
{
    public string? LastLogPath { get; set; }
    public bool LastPathIsFile { get; set; }
    public List<string> RecentPaths { get; set; } = new();
    public int LivePollingIntervalSec { get; set; } = 2;
    public List<ColumnSetting> Columns { get; set; } = DefaultColumns();
    public List<SavedFilter> SavedFilters { get; set; } = new();

    public static List<ColumnSetting> DefaultColumns() =>
    [
        new() { Name = "datetime",     Visible = true,  Order = 0 },
        new() { Name = "level",        Visible = true,  Order = 1 },
        new() { Name = "category",     Visible = true,  Order = 2 },
        new() { Name = "type",         Visible = true,  Order = 3 },
        new() { Name = "url",          Visible = true,  Order = 4 },
        new() { Name = "message",      Visible = true,  Order = 5 },
        new() { Name = "uid",          Visible = false, Order = 6 },
        new() { Name = "logger",       Visible = false, Order = 7 },
        new() { Name = "body",         Visible = false, Order = 8 },
        new() { Name = "responseTime", Visible = false, Order = 9 },
        new() { Name = "httpCode",     Visible = false, Order = 10 },
    ];
}

public class ColumnSetting
{
    public string Name { get; set; } = string.Empty;
    public bool Visible { get; set; }
    public int Order { get; set; }
}

public class SavedFilter
{
    public string Name { get; set; } = string.Empty;
    public string? Level { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Search { get; set; }
}
```

- [ ] **Step 6: Собрать**

```powershell
dotnet build src/Stoloto.LogParser.Core
```

Ожидаемый вывод: `Build succeeded.`

- [ ] **Step 7: Commit**

```powershell
git add src/Stoloto.LogParser.Core/Models/
git commit -m "feat: add core models LogEntry, LogQuery, UserSettings"
```

---

## Task 3: ILogParser + NLogJsonParser (TDD)

**Files:**
- Create: `src/Stoloto.LogParser.Core/Parsers/ILogParser.cs`
- Create: `src/Stoloto.LogParser.Core/Parsers/NLogJsonParser.cs`
- Create: `tests/Stoloto.LogParser.Core.Tests/Parsers/NLogJsonParserTests.cs`

- [ ] **Step 1: Создать ILogParser.cs**

```csharp
// src/Stoloto.LogParser.Core/Parsers/ILogParser.cs
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public interface ILogParser
{
    bool CanParse(string line);
    LogEntry? ParseLine(string line);
}
```

- [ ] **Step 2: Написать тесты**

```csharp
// tests/Stoloto.LogParser.Core.Tests/Parsers/NLogJsonParserTests.cs
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
```

- [ ] **Step 3: Запустить тесты — убедиться что падают**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "NLogJsonParserTests" -v
```

Ожидаемый вывод: ошибка компиляции (NLogJsonParser не существует).

- [ ] **Step 4: Реализовать NLogJsonParser.cs**

```csharp
// src/Stoloto.LogParser.Core/Parsers/NLogJsonParser.cs
using System.Text.Json;
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class NLogJsonParser : ILogParser
{
    private static readonly HashSet<string> KnownFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "datetime", "level", "logger", "message", "uid", "category",
        "type", "body", "url", "responseTime", "httpCode", "details", "exception"
    };

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
```

- [ ] **Step 5: Запустить тесты — убедиться что проходят**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "NLogJsonParserTests" -v
```

Ожидаемый вывод: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 6: Commit**

```powershell
git add src/Stoloto.LogParser.Core/Parsers/ tests/Stoloto.LogParser.Core.Tests/Parsers/NLogJsonParserTests.cs
git commit -m "feat: add NLogJsonParser with tests"
```

---

## Task 4: StolotoJsonParser (TDD)

**Files:**
- Create: `src/Stoloto.LogParser.Core/Parsers/StolotoJsonParser.cs`
- Create: `tests/Stoloto.LogParser.Core.Tests/Parsers/StolotoJsonParserTests.cs`

- [ ] **Step 1: Написать тесты**

```csharp
// tests/Stoloto.LogParser.Core.Tests/Parsers/StolotoJsonParserTests.cs
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
        entry!.Datetime.Should().Be(new DateTime(2026, 5, 12, 16, 47, 21, 581));
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
```

- [ ] **Step 2: Запустить тесты — убедиться что падают**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "StolotoJsonParserTests" -v
```

Ожидаемый вывод: ошибка компиляции.

- [ ] **Step 3: Реализовать StolotoJsonParser.cs**

```csharp
// src/Stoloto.LogParser.Core/Parsers/StolotoJsonParser.cs
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
```

- [ ] **Step 4: Запустить тесты**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "StolotoJsonParserTests" -v
```

Ожидаемый вывод: `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 5: Commit**

```powershell
git add src/Stoloto.LogParser.Core/Parsers/StolotoJsonParser.cs tests/Stoloto.LogParser.Core.Tests/Parsers/StolotoJsonParserTests.cs
git commit -m "feat: add StolotoJsonParser with tests"
```

---

## Task 5: PipeDelimitedParser (TDD)

**Files:**
- Create: `src/Stoloto.LogParser.Core/Parsers/PipeDelimitedParser.cs`
- Create: `tests/Stoloto.LogParser.Core.Tests/Parsers/PipeDelimitedParserTests.cs`

- [ ] **Step 1: Написать тесты**

```csharp
// tests/Stoloto.LogParser.Core.Tests/Parsers/PipeDelimitedParserTests.cs
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
        entry!.Datetime.Should().Be(new DateTime(2026, 4, 29, 12, 13, 0, 270));
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
```

- [ ] **Step 2: Запустить тесты — убедиться что падают**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "PipeDelimitedParserTests" -v
```

- [ ] **Step 3: Реализовать PipeDelimitedParser.cs**

```csharp
// src/Stoloto.LogParser.Core/Parsers/PipeDelimitedParser.cs
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class PipeDelimitedParser : ILogParser
{
    public bool CanParse(string line)
    {
        return !line.TrimStart().StartsWith('{') && line.Contains('|');
    }

    public LogEntry? ParseLine(string line)
    {
        try
        {
            var parts = line.Split('|');
            if (parts.Length < 4) return null;

            if (!DateTime.TryParseExact(
                    parts[0].Trim(),
                    ["yyyy-MM-dd HH:mm:ss.ffff", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out var dt))
                return null;

            return new LogEntry
            {
                Datetime = dt,
                Level    = Normalize(parts[1].Trim()),
                Logger   = parts[2].Trim(),
                Message  = parts[3].Trim()
            };
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
        _         => level
    };
}
```

- [ ] **Step 4: Запустить тесты**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "PipeDelimitedParserTests" -v
```

Ожидаемый вывод: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Commit**

```powershell
git add src/Stoloto.LogParser.Core/Parsers/PipeDelimitedParser.cs tests/Stoloto.LogParser.Core.Tests/Parsers/PipeDelimitedParserTests.cs
git commit -m "feat: add PipeDelimitedParser with tests"
```

---

## Task 6: LogParserFactory (TDD)

**Files:**
- Create: `src/Stoloto.LogParser.Core/Parsers/LogParserFactory.cs`
- Create: `tests/Stoloto.LogParser.Core.Tests/Parsers/LogParserFactoryTests.cs`

- [ ] **Step 1: Написать тесты**

```csharp
// tests/Stoloto.LogParser.Core.Tests/Parsers/LogParserFactoryTests.cs
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
```

- [ ] **Step 2: Запустить тесты — убедиться что падают**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "LogParserFactoryTests" -v
```

- [ ] **Step 3: Реализовать LogParserFactory.cs**

```csharp
// src/Stoloto.LogParser.Core/Parsers/LogParserFactory.cs
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Core.Parsers;

public class LogParserFactory
{
    private readonly NLogJsonParser _nlog = new();
    private readonly StolotoJsonParser _stoloto = new();
    private readonly PipeDelimitedParser _pipe = new();

    public (LogEntry? entry, SkippedLine? skipped) TryParse(string line, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(line))
            return (null, null);

        ILogParser? parser = null;
        if (_stoloto.CanParse(line))      parser = _stoloto;
        else if (_nlog.CanParse(line))    parser = _nlog;
        else if (_pipe.CanParse(line))    parser = _pipe;

        if (parser != null)
        {
            var entry = parser.ParseLine(line);
            if (entry != null) return (entry, null);
        }

        return (null, new SkippedLine { LineNumber = lineNumber, RawText = line });
    }
}
```

- [ ] **Step 4: Запустить тесты**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "LogParserFactoryTests" -v
```

Ожидаемый вывод: `Passed! - Failed: 0, Passed: 6`

- [ ] **Step 5: Запустить все тесты Core**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ -v
```

Ожидаемый вывод: все тесты зелёные.

- [ ] **Step 6: Commit**

```powershell
git add src/Stoloto.LogParser.Core/Parsers/LogParserFactory.cs tests/Stoloto.LogParser.Core.Tests/Parsers/LogParserFactoryTests.cs
git commit -m "feat: add LogParserFactory with format detection"
```

---

## Task 7: LocalLogSource (TDD)

**Files:**
- Create: `src/Stoloto.LogParser.Core/Sources/LocalLogSource.cs`
- Create: `tests/Stoloto.LogParser.Core.Tests/Sources/LocalLogSourceTests.cs`

- [ ] **Step 1: Написать тесты**

```csharp
// tests/Stoloto.LogParser.Core.Tests/Sources/LocalLogSourceTests.cs
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
```

- [ ] **Step 2: Запустить тесты — убедиться что падают**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "LocalLogSourceTests" -v
```

- [ ] **Step 3: Реализовать LocalLogSource.cs**

```csharp
// src/Stoloto.LogParser.Core/Sources/LocalLogSource.cs
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
```

- [ ] **Step 4: Запустить тесты**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ --filter "LocalLogSourceTests" -v
```

Ожидаемый вывод: `Passed! - Failed: 0, Passed: 4`

- [ ] **Step 5: Запустить все тесты**

```powershell
dotnet test tests/Stoloto.LogParser.Core.Tests/ -v
```

- [ ] **Step 6: Commit**

```powershell
git add src/Stoloto.LogParser.Core/Sources/ tests/Stoloto.LogParser.Core.Tests/Sources/
git commit -m "feat: add LocalLogSource with folder and file reading"
```

---

## Task 8: SettingsService

**Files:**
- Create: `src/Stoloto.LogParser.Web/Services/SettingsService.cs`

- [ ] **Step 1: Реализовать SettingsService.cs**

```csharp
// src/Stoloto.LogParser.Web/Services/SettingsService.cs
using System.Text.Json;
using Stoloto.LogParser.Core.Models;

namespace Stoloto.LogParser.Web.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "appsettings.user.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public UserSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new UserSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOpts) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        if (settings.RecentPaths.Count > 10)
            settings.RecentPaths = settings.RecentPaths.Take(10).ToList();

        var json = JsonSerializer.Serialize(settings, JsonOpts);
        File.WriteAllText(SettingsPath, json);
    }

    public void AddRecentPath(string path)
    {
        var settings = Load();
        settings.RecentPaths.Remove(path);
        settings.RecentPaths.Insert(0, path);
        settings.LastLogPath = path;
        Save(settings);
    }
}
```

- [ ] **Step 2: Собрать Web-проект**

```powershell
dotnet build src/Stoloto.LogParser.Web
```

Ожидаемый вывод: `Build succeeded.`

- [ ] **Step 3: Commit**

```powershell
git add src/Stoloto.LogParser.Web/Services/SettingsService.cs
git commit -m "feat: add SettingsService with JSON persistence"
```

---

## Task 9: LogQueryService

**Files:**
- Create: `src/Stoloto.LogParser.Web/Services/LogQueryService.cs`

- [ ] **Step 1: Реализовать LogQueryService.cs**

```csharp
// src/Stoloto.LogParser.Web/Services/LogQueryService.cs
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

    public async Task<TraceResult> TraceAsync(string uid, string path, bool isFile, DateTime? date, CancellationToken ct = default)
    {
        var entries = new List<LogEntry>();
        var dateFrom = date?.Date;
        var dateTo = date?.Date;

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

public class TraceResult
{
    public List<LogEntry> Entries { get; set; } = new();
    public decimal TotalDurationMs { get; set; }
}
```

- [ ] **Step 2: Собрать**

```powershell
dotnet build src/Stoloto.LogParser.Web
```

- [ ] **Step 3: Commit**

```powershell
git add src/Stoloto.LogParser.Web/Services/LogQueryService.cs
git commit -m "feat: add LogQueryService with filtering and pagination"
```

---

## Task 10: LiveLogService (SSE polling)

**Files:**
- Create: `src/Stoloto.LogParser.Web/Services/LiveLogService.cs`

- [ ] **Step 1: Реализовать LiveLogService.cs**

```csharp
// src/Stoloto.LogParser.Web/Services/LiveLogService.cs
using System.Collections.Concurrent;
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
        return Path.Combine(folder, $"Lmfs.Api.{today}.log");
    }
}
```

- [ ] **Step 2: Собрать**

```powershell
dotnet build src/Stoloto.LogParser.Web
```

- [ ] **Step 3: Commit**

```powershell
git add src/Stoloto.LogParser.Web/Services/LiveLogService.cs
git commit -m "feat: add LiveLogService with SSE polling"
```

---

## Task 11: Controllers + Program.cs

**Files:**
- Create: `src/Stoloto.LogParser.Web/Controllers/LogsController.cs`
- Create: `src/Stoloto.LogParser.Web/Controllers/SettingsController.cs`
- Modify: `src/Stoloto.LogParser.Web/Program.cs`

- [ ] **Step 1: Создать LogsController.cs**

```csharp
// src/Stoloto.LogParser.Web/Controllers/LogsController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Web.Services;

namespace Stoloto.LogParser.Web.Controllers;

[ApiController]
[Route("api/logs")]
public class LogsController(LogQueryService queryService, LiveLogService liveService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo = null,
        [FromQuery] string? levels = null,
        [FromQuery] string? categories = null,
        [FromQuery] string? type = null,
        [FromQuery] string? uid = null,
        [FromQuery] string? urlContains = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path) || (!isFile && !Directory.Exists(path)) || (isFile && !File.Exists(path)))
            return BadRequest(new { error = $"Путь не найден: {path}" });

        var query = new LogQuery
        {
            Path = path,
            IsFile = isFile,
            DateFrom = dateFrom != null ? DateTime.Parse(dateFrom) : null,
            DateTo = dateTo != null ? DateTime.Parse(dateTo) : null,
            Levels = levels?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Categories = categories?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
            Type = type,
            Uid = uid,
            UrlContains = urlContains,
            Search = search,
            Page = page,
            PageSize = pageSize
        };

        var result = await queryService.QueryAsync(query, ct);
        return Ok(result);
    }

    [HttpGet("trace/{uid}")]
    public async Task<IActionResult> Trace(
        string uid,
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? date = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(path))
            return BadRequest(new { error = "Укажите path" });

        DateTime? dateValue = date != null ? DateTime.Parse(date) : null;
        var result = await queryService.TraceAsync(uid, path, isFile, dateValue, ct);
        return Ok(result);
    }

    [HttpGet("live")]
    public async Task Live(
        [FromQuery] string path,
        [FromQuery] bool isFile = false,
        [FromQuery] string? levels = null,
        [FromQuery] string? categories = null,
        CancellationToken ct = default)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var levelList = levels?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];
        var categoryList = categories?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [];

        var sessionId = liveService.AddSession(path, isFile, levelList, categoryList);
        var session = liveService.GetSession(sessionId)!;

        try
        {
            await foreach (var entries in session.Channel.Reader.ReadAllAsync(ct))
            {
                var json = JsonSerializer.Serialize(new { entries }, JsonOpts);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        finally
        {
            liveService.RemoveSession(sessionId);
        }
    }
}
```

- [ ] **Step 2: Создать SettingsController.cs**

```csharp
// src/Stoloto.LogParser.Web/Controllers/SettingsController.cs
using Microsoft.AspNetCore.Mvc;
using Stoloto.LogParser.Core.Models;
using Stoloto.LogParser.Web.Services;

namespace Stoloto.LogParser.Web.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(SettingsService settingsService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(settingsService.Load());

    [HttpPut]
    public IActionResult Put([FromBody] UserSettings settings)
    {
        settingsService.Save(settings);
        return Ok(settingsService.Load());
    }
}
```

- [ ] **Step 3: Заменить Program.cs**

```csharp
// src/Stoloto.LogParser.Web/Program.cs
using Stoloto.LogParser.Core.Parsers;
using Stoloto.LogParser.Core.Sources;
using Stoloto.LogParser.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<LogParserFactory>();
builder.Services.AddSingleton<LocalLogSource>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddScoped<LogQueryService>();
builder.Services.AddSingleton<LiveLogService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveLogService>());
builder.Services.AddControllers();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
```

- [ ] **Step 4: Собрать**

```powershell
dotnet build src/Stoloto.LogParser.Web
```

Ожидаемый вывод: `Build succeeded.`

- [ ] **Step 5: Commit**

```powershell
git add src/Stoloto.LogParser.Web/Controllers/ src/Stoloto.LogParser.Web/Program.cs
git commit -m "feat: add controllers and configure DI in Program.cs"
```

---

## Task 12: index.html — основной UI

**Files:**
- Create: `src/Stoloto.LogParser.Web/wwwroot/index.html`
- Create: `src/Stoloto.LogParser.Web/wwwroot/trace.html`
- Create: `src/Stoloto.LogParser.Web/wwwroot/settings.html`

- [ ] **Step 1: Создать index.html**

```html
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>LMFS Log Parser</title>
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css">
  <style>
    body { font-size: 0.875rem; }
    #sidebar { width: 180px; min-width: 180px; border-right: 1px solid #dee2e6; padding: 0.75rem; }
    #main { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
    #table-wrap { overflow: auto; flex: 1; }
    .log-row { cursor: pointer; }
    .log-row:hover { background: #f8f9fa; }
    .level-Info    { color: #0d6efd; }
    .level-Warn    { color: #fd7e14; }
    .level-Error   { color: #dc3545; font-weight: 600; }
    .level-Debug   { color: #6c757d; }
    .detail-card   { background: #f8f9fa; border-left: 4px solid #0d6efd; padding: 0.75rem; font-size: 0.8rem; }
    .filter-tag    { display: inline-flex; align-items: center; gap: 4px; }
    #skipped-btn   { display: none; }
    .cat-api      { color: #0d6efd; }
    .cat-kkt      { color: #fd7e14; }
    .cat-db       { color: #6c757d; }
    .cat-auth     { color: #198754; }
    .cat-pos      { color: #6f42c1; }
    .cat-queue    { color: #0dcaf0; }
    .cat-schedule { color: #d63384; }
    .cat-file     { color: #795548; }
  </style>
</head>
<body class="d-flex flex-column vh-100">

<!-- Navbar -->
<nav class="navbar navbar-dark bg-dark px-3 py-2 flex-shrink-0">
  <span class="navbar-brand mb-0 h6">LMFS Log Parser</span>
  <div class="d-flex align-items-center gap-2">
    <button class="btn btn-sm btn-outline-light" id="path-btn">
      📁 <span id="path-label">Выбрать папку...</span>
    </button>
    <div class="form-check form-switch mb-0 text-white d-flex align-items-center gap-1">
      <input class="form-check-input" type="checkbox" id="live-toggle">
      <label class="form-check-label" for="live-toggle">Live</label>
      <span id="live-dot" class="text-danger d-none">●</span>
    </div>
    <a href="/settings.html" class="btn btn-sm btn-outline-secondary">⚙</a>
  </div>
</nav>

<!-- Filters -->
<div class="bg-light border-bottom px-3 py-2 flex-shrink-0">
  <div class="row g-2 align-items-end">
    <div class="col-auto">
      <label class="form-label mb-0 small">Дата от</label>
      <input type="date" class="form-control form-control-sm" id="date-from">
    </div>
    <div class="col-auto">
      <label class="form-label mb-0 small">до</label>
      <input type="date" class="form-control form-control-sm" id="date-to">
    </div>
    <div class="col-auto">
      <label class="form-label mb-0 small">Уровень</label>
      <select class="form-select form-select-sm" id="level-select" multiple style="height:60px">
        <option>Info</option><option>Warn</option><option>Error</option><option>Debug</option>
      </select>
    </div>
    <div class="col-auto">
      <label class="form-label mb-0 small">Категория</label>
      <select class="form-select form-select-sm" id="category-select" multiple style="height:60px">
        <option>api</option><option>kkt</option><option>pos</option><option>db</option>
        <option>auth</option><option>schedule</option><option>queue</option><option>file</option>
      </select>
    </div>
    <div class="col-auto">
      <label class="form-label mb-0 small">Тип</label>
      <select class="form-select form-select-sm" id="type-select">
        <option value="">Все</option><option>request</option><option>response</option>
      </select>
    </div>
    <div class="col-auto">
      <label class="form-label mb-0 small">UID</label>
      <input type="text" class="form-control form-control-sm" id="uid-input" placeholder="cae313b9...">
    </div>
    <div class="col-auto">
      <label class="form-label mb-0 small">URL</label>
      <input type="text" class="form-control form-control-sm" id="url-input" placeholder="FiscalCheck...">
    </div>
    <div class="col">
      <label class="form-label mb-0 small">Поиск</label>
      <input type="text" class="form-control form-control-sm" id="search-input" placeholder="текст в message/body...">
    </div>
    <div class="col-auto">
      <button class="btn btn-sm btn-primary" id="load-btn">Загрузить</button>
      <button class="btn btn-sm btn-outline-secondary" id="reset-btn">Сбросить</button>
    </div>
  </div>
  <!-- Active tags row -->
  <div id="active-tags" class="mt-1 d-flex flex-wrap gap-1"></div>
  <!-- Saved filters row -->
  <div id="saved-filters-row" class="mt-1 d-flex flex-wrap gap-1 align-items-center">
    <small class="text-muted">Фильтры:</small>
    <div id="saved-filters-list" class="d-flex flex-wrap gap-1"></div>
    <button class="btn btn-sm btn-outline-success" id="save-filter-btn">+ Сохранить</button>
  </div>
</div>

<!-- Body -->
<div class="d-flex flex-grow-1 overflow-hidden">

  <!-- Sidebar: columns -->
  <div id="sidebar">
    <div class="fw-semibold small mb-2">Колонки</div>
    <div id="column-list"></div>
  </div>

  <!-- Main area -->
  <div id="main" class="p-0">
    <!-- Toolbar -->
    <div class="d-flex align-items-center gap-2 px-2 py-1 border-bottom bg-white flex-shrink-0">
      <small class="text-muted" id="total-label">Записей: 0</small>
      <button class="btn btn-sm btn-warning d-none" id="skipped-btn">⚠ Пропущено <span id="skipped-count">0</span> строк</button>
      <div class="ms-auto d-flex align-items-center gap-2" id="live-controls" style="display:none!important">
        <button class="btn btn-sm btn-outline-warning d-none" id="pause-btn">⏸ Пауза</button>
        <span class="badge bg-warning text-dark d-none" id="missed-badge">Пропущено: 0</span>
      </div>
    </div>

    <!-- Table -->
    <div id="table-wrap">
      <table class="table table-sm table-hover table-bordered mb-0" id="log-table">
        <thead class="table-light sticky-top">
          <tr id="thead-row"></tr>
        </thead>
        <tbody id="tbody"></tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div class="d-flex align-items-center gap-2 px-2 py-1 border-top flex-shrink-0">
      <button class="btn btn-sm btn-outline-secondary" id="prev-btn">←</button>
      <span id="page-label" class="small">Стр. 1</span>
      <button class="btn btn-sm btn-outline-secondary" id="next-btn">→</button>
      <select class="form-select form-select-sm" id="page-size-select" style="width:80px">
        <option value="50">50</option>
        <option value="100" selected>100</option>
        <option value="200">200</option>
      </select>
    </div>
  </div>
</div>

<!-- Source Modal -->
<div class="modal fade" id="source-modal" tabindex="-1">
  <div class="modal-dialog">
    <div class="modal-content">
      <div class="modal-header">
        <h6 class="modal-title">Источник логов</h6>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body">
        <div class="mb-2">
          <div class="form-check form-check-inline">
            <input class="form-check-input" type="radio" name="path-mode" id="mode-folder" value="folder" checked>
            <label class="form-check-label" for="mode-folder">Папка</label>
          </div>
          <div class="form-check form-check-inline">
            <input class="form-check-input" type="radio" name="path-mode" id="mode-file" value="file">
            <label class="form-check-label" for="mode-file">Файл</label>
          </div>
        </div>
        <div class="input-group mb-2">
          <input type="text" class="form-control" id="path-input" placeholder="C:\Frame\...\logs">
        </div>
        <div id="recent-list" class="list-group list-group-flush small"></div>
        <div id="files-found" class="text-muted small mt-1"></div>
      </div>
      <div class="modal-footer">
        <button class="btn btn-secondary btn-sm" data-bs-dismiss="modal">Отмена</button>
        <button class="btn btn-primary btn-sm" id="apply-path-btn">Применить</button>
      </div>
    </div>
  </div>
</div>

<!-- Skipped Lines Modal -->
<div class="modal fade" id="skipped-modal" tabindex="-1">
  <div class="modal-dialog modal-lg">
    <div class="modal-content">
      <div class="modal-header">
        <h6 class="modal-title">Нераспознанные строки (<span id="skipped-modal-count">0</span>)</h6>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body">
        <div class="list-group list-group-flush mb-2" id="skipped-list" style="max-height:300px;overflow:auto"></div>
        <div class="border rounded p-2 bg-light small font-monospace" id="skipped-detail" style="white-space:pre-wrap;min-height:60px"></div>
      </div>
      <div class="modal-footer">
        <button class="btn btn-secondary btn-sm" data-bs-dismiss="modal">Закрыть</button>
      </div>
    </div>
  </div>
</div>

<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
<script type="module" src="/js/app.js"></script>
</body>
</html>
```

- [ ] **Step 2: Создать trace.html**

```html
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>Trace — LMFS Log Parser</title>
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css">
  <style>
    .bar-row { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
    .bar-label { width: 90px; font-size: 0.75rem; text-align: right; flex-shrink: 0; }
    .bar-track { flex: 1; height: 24px; background: #e9ecef; border-radius: 4px; position: relative; cursor: pointer; }
    .bar-fill { height: 100%; border-radius: 4px; position: absolute; }
    .bar-dur { font-size: 0.7rem; color: #6c757d; width: 60px; flex-shrink: 0; }
    .cat-api      { background: #0d6efd; }
    .cat-kkt      { background: #fd7e14; }
    .cat-db       { background: #6c757d; }
    .cat-auth     { background: #198754; }
    .cat-pos      { background: #6f42c1; }
    .cat-queue    { background: #0dcaf0; }
    .cat-schedule { background: #d63384; }
    .cat-file     { background: #795548; }
    .detail-card  { background: #f8f9fa; border-left: 4px solid #0d6efd; padding: 0.75rem; font-size: 0.8rem; }
  </style>
</head>
<body class="p-3">
  <div class="d-flex align-items-center gap-2 mb-3">
    <a href="/" class="btn btn-sm btn-outline-secondary">← Назад</a>
    <h6 class="mb-0" id="trace-title">Trace</h6>
  </div>
  <div id="summary" class="mb-3 text-muted small"></div>
  <div id="timeline"></div>
  <div id="detail" class="mt-3"></div>
<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
<script type="module" src="/js/trace.js"></script>
</body>
</html>
```

- [ ] **Step 3: Создать settings.html**

```html
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8">
  <title>Настройки — LMFS Log Parser</title>
  <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css">
  <style>
    .drag-handle { cursor: grab; color: #aaa; }
    .drag-handle:active { cursor: grabbing; }
    .col-item.drag-over { border-top: 2px solid #0d6efd; }
  </style>
</head>
<body class="p-4" style="max-width:700px">
  <div class="d-flex align-items-center gap-2 mb-4">
    <a href="/" class="btn btn-sm btn-outline-secondary">← Назад</a>
    <h5 class="mb-0">Настройки</h5>
  </div>

  <h6>Колонки таблицы</h6>
  <div id="column-list" class="list-group mb-4"></div>

  <h6>Live-режим</h6>
  <div class="mb-4 d-flex align-items-center gap-2">
    <label class="form-label mb-0">Интервал опроса (сек):</label>
    <input type="number" class="form-control form-control-sm" id="interval-input" style="width:80px" min="1" max="60">
  </div>

  <h6>Сохранённые фильтры</h6>
  <div id="saved-filters" class="mb-2"></div>
  <button class="btn btn-sm btn-outline-success mb-4" id="add-filter-btn">+ Добавить</button>

  <h6>Последние пути</h6>
  <div id="recent-paths" class="mb-4"></div>

  <div class="d-flex gap-2">
    <button class="btn btn-outline-danger btn-sm" id="reset-btn">Сбросить всё</button>
    <button class="btn btn-primary btn-sm ms-auto" id="save-btn">Сохранить</button>
  </div>

<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js"></script>
<script type="module" src="/js/settings.js"></script>
</body>
</html>
```

- [ ] **Step 4: Commit**

```powershell
git add src/Stoloto.LogParser.Web/wwwroot/
git commit -m "feat: add HTML pages (index, trace, settings)"
```

---

## Task 13: api.js

**Files:**
- Create: `src/Stoloto.LogParser.Web/wwwroot/js/api.js`

- [ ] **Step 1: Создать api.js**

```js
// src/Stoloto.LogParser.Web/wwwroot/js/api.js

export async function getLogs(params) {
  const q = new URLSearchParams();
  if (params.path)        q.set('path', params.path);
  if (params.isFile)      q.set('isFile', 'true');
  if (params.dateFrom)    q.set('dateFrom', params.dateFrom);
  if (params.dateTo)      q.set('dateTo', params.dateTo);
  if (params.levels?.length)      q.set('levels', params.levels.join(','));
  if (params.categories?.length)  q.set('categories', params.categories.join(','));
  if (params.type)        q.set('type', params.type);
  if (params.uid)         q.set('uid', params.uid);
  if (params.urlContains) q.set('urlContains', params.urlContains);
  if (params.search)      q.set('search', params.search);
  q.set('page',     params.page     ?? 1);
  q.set('pageSize', params.pageSize ?? 100);

  const res = await fetch(`/api/logs?${q}`);
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: res.statusText }));
    throw new Error(err.error ?? res.statusText);
  }
  return res.json();
}

export async function getTrace(uid, path, isFile, date) {
  const q = new URLSearchParams({ path, uid });
  if (isFile) q.set('isFile', 'true');
  if (date)   q.set('date', date);
  const res = await fetch(`/api/logs/trace/${uid}?${q}`);
  if (!res.ok) throw new Error(res.statusText);
  return res.json();
}

export function connectLive(params, onData, onError) {
  const q = new URLSearchParams();
  q.set('path', params.path);
  if (params.isFile)     q.set('isFile', 'true');
  if (params.levels?.length)     q.set('levels', params.levels.join(','));
  if (params.categories?.length) q.set('categories', params.categories.join(','));

  const es = new EventSource(`/api/logs/live?${q}`);
  es.onmessage = e => { try { onData(JSON.parse(e.data)); } catch {} };
  es.onerror   = onError ?? (() => {});
  return es;
}

export async function getSettings() {
  const res = await fetch('/api/settings');
  return res.json();
}

export async function saveSettings(settings) {
  const res = await fetch('/api/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings)
  });
  return res.json();
}
```

- [ ] **Step 2: Commit**

```powershell
git add src/Stoloto.LogParser.Web/wwwroot/js/api.js
git commit -m "feat: add api.js fetch wrappers"
```

---

## Task 14: app.js — главная логика

**Files:**
- Create: `src/Stoloto.LogParser.Web/wwwroot/js/app.js`

- [ ] **Step 1: Создать app.js**

```js
// src/Stoloto.LogParser.Web/wwwroot/js/app.js
import { getLogs, connectLive, getSettings, saveSettings } from './api.js';

let state = {
  path: '', isFile: false, page: 1, pageSize: 100,
  skippedLines: [], settings: null,
  liveEs: null, livePaused: false, missedCount: 0
};

const $ = id => document.getElementById(id);

// ── Init ────────────────────────────────────────────────────────────────────
async function init() {
  state.settings = await getSettings();
  renderColumns();
  renderSavedFilters();
  renderRecentPaths();
  if (state.settings.lastLogPath) {
    state.path   = state.settings.lastLogPath;
    state.isFile = state.settings.lastPathIsFile;
    $('path-label').textContent = shortPath(state.path);
  }
  const today = new Date().toISOString().slice(0, 10);
  $('date-from').value = today;
  $('date-to').value   = today;
  bindEvents();
}

// ── Columns ──────────────────────────────────────────────────────────────────
function renderColumns() {
  const list = $('column-list');
  list.innerHTML = '';
  const cols = state.settings.columns.sort((a, b) => a.order - b.order);
  cols.forEach(col => {
    const d = document.createElement('div');
    d.className = 'form-check';
    d.innerHTML = `<input class="form-check-input" type="checkbox" id="col-${col.name}" ${col.visible ? 'checked' : ''}>
      <label class="form-check-label small" for="col-${col.name}">${col.name}</label>`;
    d.querySelector('input').addEventListener('change', async e => {
      col.visible = e.target.checked;
      await saveSettings(state.settings);
      if (state.lastResult) renderTable(state.lastResult);
    });
    list.appendChild(d);
  });
}

function visibleCols() {
  return state.settings.columns
    .filter(c => c.visible)
    .sort((a, b) => a.order - b.order)
    .map(c => c.name);
}

// ── Table ────────────────────────────────────────────────────────────────────
function renderTable(result) {
  state.lastResult = result;
  const cols = visibleCols();

  const thead = $('thead-row');
  thead.innerHTML = cols.map(c => `<th class="text-nowrap">${c}</th>`).join('');

  const tbody = $('tbody');
  tbody.innerHTML = '';
  result.items.forEach((entry, i) => {
    const tr = document.createElement('tr');
    tr.className = 'log-row';
    tr.dataset.idx = i;
    tr.innerHTML = cols.map(c => {
      const val = cellValue(entry, c);
      let cls = '';
      if (c === 'level') cls = `level-${entry.level}`;
      if (c === 'category') cls = `cat-${entry.category}`;
      return `<td class="${cls} text-nowrap" style="max-width:300px;overflow:hidden;text-overflow:ellipsis" title="${esc(val)}">${esc(val)}</td>`;
    }).join('');
    tr.addEventListener('click', () => toggleDetail(tr, entry, result.items));
    tbody.appendChild(tr);
  });

  $('total-label').textContent = `Записей: ${result.total}`;
  $('page-label').textContent  = `Стр. ${result.page}`;
  $('prev-btn').disabled = result.page <= 1;
  $('next-btn').disabled = result.page * result.pageSize >= result.total;

  const sk = result.skippedLines ?? [];
  state.skippedLines = sk;
  const btn = $('skipped-btn');
  if (sk.length > 0) {
    btn.classList.remove('d-none');
    $('skipped-count').textContent = sk.length;
  } else {
    btn.classList.add('d-none');
  }
}

function cellValue(entry, col) {
  if (col === 'datetime') return entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : '';
  return entry[col] ?? entry.extra?.[col] ?? '';
}

function toggleDetail(tr, entry, all) {
  const existing = tr.nextElementSibling;
  if (existing?.classList.contains('detail-row')) { existing.remove(); return; }

  const detail = document.createElement('tr');
  detail.className = 'detail-row';
  const fields = [
    ['datetime', new Date(entry.datetime).toLocaleString('ru')],
    ['level', entry.level],
    ['category', entry.category],
    ['type', entry.type],
    ['url', entry.url],
    ['uid', entry.uid],
    ['message', entry.message],
    ['body', entry.body],
    ['responseTime', entry.responseTime != null ? entry.responseTime + 's' : ''],
    ['httpCode', entry.httpCode],
    ['logger', entry.logger],
    ['sourceFile', entry.sourceFile],
    ...Object.entries(entry.extra ?? {})
  ].filter(([, v]) => v != null && v !== '');

  const rows = fields.map(([k, v]) => {
    const traceLink = k === 'uid' && v
      ? ` <a href="/trace.html?uid=${encodeURIComponent(v)}&path=${encodeURIComponent(state.path)}&isFile=${state.isFile}&date=${entry.datetime?.slice(0,10) ?? ''}" target="_blank">[→ trace]</a>`
      : '';
    return `<tr><td class="text-muted" style="width:130px">${esc(k)}</td><td style="word-break:break-all">${esc(String(v))}${traceLink}</td></tr>`;
  }).join('');

  detail.innerHTML = `<td colspan="99"><div class="detail-card"><table class="table table-sm mb-0">${rows}</table></div></td>`;
  tr.after(detail);
}

// ── Load ─────────────────────────────────────────────────────────────────────
async function loadLogs() {
  if (!state.path) { alert('Укажите путь к папке или файлу'); return; }

  const params = buildQuery();
  try {
    const result = await getLogs(params);
    renderTable(result);
    await saveSettings({ ...state.settings, lastLogPath: state.path, lastPathIsFile: state.isFile });
  } catch (e) {
    alert('Ошибка: ' + e.message);
  }
}

function buildQuery() {
  const levels     = Array.from($('level-select').selectedOptions).map(o => o.value);
  const categories = Array.from($('category-select').selectedOptions).map(o => o.value);
  return {
    path: state.path, isFile: state.isFile,
    dateFrom: $('date-from').value, dateTo: $('date-to').value,
    levels, categories,
    type: $('type-select').value,
    uid: $('uid-input').value,
    urlContains: $('url-input').value,
    search: $('search-input').value,
    page: state.page, pageSize: Number($('page-size-select').value)
  };
}

// ── Live ─────────────────────────────────────────────────────────────────────
function startLive() {
  if (!state.path) { $('live-toggle').checked = false; return; }
  state.livePaused = false;
  state.missedCount = 0;
  $('live-dot').classList.remove('d-none');
  $('pause-btn').classList.remove('d-none');
  $('live-controls').style.removeProperty('display');

  const params = buildQuery();
  state.liveEs = connectLive(params, data => {
    if (state.livePaused) { state.missedCount += data.entries?.length ?? 0; updateMissed(); return; }
    prependRows(data.entries ?? []);
  }, () => setTimeout(startLive, 3000));
}

function stopLive() {
  state.liveEs?.close();
  state.liveEs = null;
  $('live-dot').classList.add('d-none');
  $('pause-btn').classList.add('d-none');
  $('missed-badge').classList.add('d-none');
}

function prependRows(entries) {
  const cols = visibleCols();
  const tbody = $('tbody');
  entries.forEach(entry => {
    const tr = document.createElement('tr');
    tr.className = 'log-row table-warning';
    tr.innerHTML = cols.map(c => `<td class="text-nowrap">${esc(cellValue(entry, c))}</td>`).join('');
    tr.addEventListener('click', () => toggleDetail(tr, entry, entries));
    tbody.prepend(tr);
  });
}

function updateMissed() {
  const b = $('missed-badge');
  if (state.missedCount > 0) { b.textContent = `Пропущено: ${state.missedCount}`; b.classList.remove('d-none'); }
  else b.classList.add('d-none');
}

// ── Saved filters ─────────────────────────────────────────────────────────────
function renderSavedFilters() {
  const list = $('saved-filters-list');
  list.innerHTML = '';
  (state.settings.savedFilters ?? []).forEach((f, i) => {
    const btn = document.createElement('button');
    btn.className = 'btn btn-sm btn-outline-info';
    btn.textContent = f.name;
    btn.addEventListener('click', () => applyFilter(f));
    list.appendChild(btn);
  });
}

function applyFilter(f) {
  if (f.level) Array.from($('level-select').options).forEach(o => o.selected = o.value === f.level);
  if (f.category) Array.from($('category-select').options).forEach(o => o.selected = o.value === f.category);
  if (f.type) $('type-select').value = f.type;
  if (f.search) $('search-input').value = f.search;
  loadLogs();
}

// ── Recent paths ──────────────────────────────────────────────────────────────
function renderRecentPaths() {
  const list = $('recent-list');
  list.innerHTML = '';
  (state.settings.recentPaths ?? []).forEach(p => {
    const a = document.createElement('a');
    a.className = 'list-group-item list-group-item-action small py-1';
    a.textContent = p;
    a.href = '#';
    a.addEventListener('click', e => { e.preventDefault(); $('path-input').value = p; });
    list.appendChild(a);
  });
}

// ── Skipped modal ─────────────────────────────────────────────────────────────
function showSkipped() {
  $('skipped-modal-count').textContent = state.skippedLines.length;
  const list = $('skipped-list');
  list.innerHTML = '';
  state.skippedLines.forEach(sl => {
    const a = document.createElement('a');
    a.className = 'list-group-item list-group-item-action small py-1 font-monospace';
    a.textContent = `#${sl.lineNumber}  ${sl.rawText.slice(0, 80)}`;
    a.href = '#';
    a.addEventListener('click', e => {
      e.preventDefault();
      $('skipped-detail').textContent = sl.rawText;
    });
    list.appendChild(a);
  });
  new bootstrap.Modal($('skipped-modal')).show();
}

// ── Helpers ──────────────────────────────────────────────────────────────────
function esc(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

function shortPath(p) { return p.length > 40 ? '...' + p.slice(-38) : p; }

// ── Events ───────────────────────────────────────────────────────────────────
function bindEvents() {
  $('load-btn').addEventListener('click', () => { state.page = 1; loadLogs(); });
  $('reset-btn').addEventListener('click', () => {
    $('level-select').selectedIndex = -1;
    $('category-select').selectedIndex = -1;
    $('type-select').value = '';
    $('uid-input').value = '';
    $('url-input').value = '';
    $('search-input').value = '';
  });
  $('prev-btn').addEventListener('click', () => { state.page--; loadLogs(); });
  $('next-btn').addEventListener('click', () => { state.page++; loadLogs(); });

  $('live-toggle').addEventListener('change', e => e.target.checked ? startLive() : stopLive());
  $('pause-btn').addEventListener('click', () => {
    state.livePaused = !state.livePaused;
    $('pause-btn').textContent = state.livePaused ? '▶ Продолжить' : '⏸ Пауза';
    if (!state.livePaused) { state.missedCount = 0; updateMissed(); }
  });

  $('path-btn').addEventListener('click', () => {
    renderRecentPaths();
    new bootstrap.Modal($('source-modal')).show();
  });

  $('apply-path-btn').addEventListener('click', async () => {
    const p = $('path-input').value.trim();
    if (!p) return;
    state.path   = p;
    state.isFile = document.querySelector('input[name="path-mode"]:checked').value === 'file';
    $('path-label').textContent = shortPath(p);
    bootstrap.Modal.getInstance($('source-modal')).hide();
    state.settings.recentPaths = [p, ...(state.settings.recentPaths ?? []).filter(x => x !== p)].slice(0, 10);
    state.settings.lastLogPath   = p;
    state.settings.lastPathIsFile = state.isFile;
    await saveSettings(state.settings);
    loadLogs();
  });

  $('skipped-btn').addEventListener('click', showSkipped);

  $('save-filter-btn').addEventListener('click', async () => {
    const name = prompt('Название фильтра:');
    if (!name) return;
    const levels     = Array.from($('level-select').selectedOptions).map(o => o.value);
    const categories = Array.from($('category-select').selectedOptions).map(o => o.value);
    state.settings.savedFilters.push({
      name,
      level:    levels[0] ?? null,
      category: categories[0] ?? null,
      type:     $('type-select').value || null,
      search:   $('search-input').value || null
    });
    await saveSettings(state.settings);
    renderSavedFilters();
  });
}

init();
```

- [ ] **Step 2: Commit**

```powershell
git add src/Stoloto.LogParser.Web/wwwroot/js/app.js
git commit -m "feat: add app.js main page logic"
```

---

## Task 15: trace.js — Timeline по uid

**Files:**
- Create: `src/Stoloto.LogParser.Web/wwwroot/js/trace.js`

- [ ] **Step 1: Создать trace.js**

```js
// src/Stoloto.LogParser.Web/wwwroot/js/trace.js
import { getTrace } from './api.js';

const CAT_COLORS = {
  api: '#0d6efd', kkt: '#fd7e14', db: '#6c757d', auth: '#198754',
  pos: '#6f42c1', queue: '#0dcaf0', schedule: '#d63384', file: '#795548'
};

function esc(s) {
  return String(s ?? '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

async function init() {
  const p = new URLSearchParams(location.search);
  const uid    = p.get('uid');
  const path   = p.get('path');
  const isFile = p.get('isFile') === 'true';
  const date   = p.get('date');

  if (!uid || !path) {
    document.getElementById('trace-title').textContent = 'Нет параметров';
    return;
  }

  document.getElementById('trace-title').textContent = `Trace: ${uid}`;

  let result;
  try {
    result = await getTrace(uid, path, isFile, date);
  } catch (e) {
    document.getElementById('summary').textContent = 'Ошибка: ' + e.message;
    return;
  }

  const { entries, totalDurationMs } = result;
  if (!entries.length) {
    document.getElementById('summary').textContent = 'Записей не найдено';
    return;
  }

  const first = new Date(entries[0].datetime).getTime();
  const last  = new Date(entries[entries.length - 1].datetime).getTime();
  const span  = last - first || 1;

  document.getElementById('summary').innerHTML =
    `Шагов: <strong>${entries.length}</strong> &nbsp; Общее время: <strong>${totalDurationMs.toFixed(0)} ms</strong>`;

  const timeline = document.getElementById('timeline');
  timeline.innerHTML = '';

  entries.forEach((entry, i) => {
    const start = new Date(entry.datetime).getTime() - first;
    const dur   = entry.responseTime != null ? entry.responseTime * 1000 : 0;
    const left  = (start / span) * 100;
    const width = Math.max((dur / span) * 100, 0.5);
    const color = CAT_COLORS[entry.category] ?? '#adb5bd';
    const label = entry.category ?? entry.logger?.split('.').pop() ?? '?';
    const durLabel = dur > 0 ? `${dur.toFixed(0)}ms` : '';

    const row = document.createElement('div');
    row.className = 'bar-row';
    row.innerHTML = `
      <div class="bar-label text-muted">${esc(label)}</div>
      <div class="bar-track" title="${esc(entry.url ?? entry.message ?? '')}">
        <div class="bar-fill" style="left:${left}%;width:${Math.min(width, 100 - left)}%;background:${color}"></div>
      </div>
      <div class="bar-dur">${durLabel}</div>`;

    row.querySelector('.bar-track').addEventListener('click', () => showDetail(entry, i));
    timeline.appendChild(row);
  });
}

function showDetail(entry, i) {
  const detail = document.getElementById('detail');
  const fields = [
    ['datetime', entry.datetime ? new Date(entry.datetime).toLocaleString('ru') : ''],
    ['level',    entry.level],
    ['category', entry.category],
    ['type',     entry.type],
    ['url',      entry.url],
    ['uid',      entry.uid],
    ['message',  entry.message],
    ['body',     entry.body],
    ['responseTime', entry.responseTime != null ? entry.responseTime + 's' : null],
    ['httpCode', entry.httpCode],
    ['logger',   entry.logger],
    ...Object.entries(entry.extra ?? {})
  ].filter(([, v]) => v != null && v !== '');

  const rows = fields.map(([k, v]) =>
    `<tr><td class="text-muted" style="width:130px">${esc(k)}</td><td style="word-break:break-all">${esc(String(v))}</td></tr>`
  ).join('');

  detail.innerHTML = `<div class="detail-card"><table class="table table-sm mb-0">${rows}</table></div>`;
}

init();
```

- [ ] **Step 2: Commit**

```powershell
git add src/Stoloto.LogParser.Web/wwwroot/js/trace.js
git commit -m "feat: add trace.js waterfall timeline"
```

---

## Task 16: settings.js

**Files:**
- Create: `src/Stoloto.LogParser.Web/wwwroot/js/settings.js`

- [ ] **Step 1: Создать settings.js**

```js
// src/Stoloto.LogParser.Web/wwwroot/js/settings.js
import { getSettings, saveSettings } from './api.js';

let settings = null;
let dragSrc  = null;

async function init() {
  settings = await getSettings();
  renderColumns();
  document.getElementById('interval-input').value = settings.livePollingIntervalSec ?? 2;
  renderSavedFilters();
  renderRecentPaths();

  document.getElementById('save-btn').addEventListener('click', save);
  document.getElementById('reset-btn').addEventListener('click', async () => {
    if (!confirm('Сбросить все настройки?')) return;
    settings.columns      = null;
    settings.savedFilters = [];
    settings.recentPaths  = [];
    await saveSettings(settings);
    location.reload();
  });
  document.getElementById('add-filter-btn').addEventListener('click', addFilter);
}

function renderColumns() {
  const list = document.getElementById('column-list');
  list.innerHTML = '';
  settings.columns.sort((a, b) => a.order - b.order).forEach((col, i) => {
    const item = document.createElement('div');
    item.className = 'list-group-item col-item d-flex align-items-center gap-2';
    item.draggable = true;
    item.dataset.name = col.name;
    item.innerHTML = `
      <span class="drag-handle">⠿</span>
      <input type="checkbox" class="form-check-input" ${col.visible ? 'checked' : ''}>
      <span>${col.name}</span>`;
    item.querySelector('input').addEventListener('change', e => { col.visible = e.target.checked; });
    item.addEventListener('dragstart', e => { dragSrc = item; e.dataTransfer.effectAllowed = 'move'; });
    item.addEventListener('dragover', e => { e.preventDefault(); item.classList.add('drag-over'); });
    item.addEventListener('dragleave', () => item.classList.remove('drag-over'));
    item.addEventListener('drop', e => {
      e.preventDefault();
      item.classList.remove('drag-over');
      if (dragSrc === item) return;
      list.insertBefore(dragSrc, item);
      reorderColumns();
    });
    list.appendChild(item);
  });
}

function reorderColumns() {
  const items = document.querySelectorAll('.col-item');
  items.forEach((item, i) => {
    const col = settings.columns.find(c => c.name === item.dataset.name);
    if (col) col.order = i;
  });
}

function renderSavedFilters() {
  const el = document.getElementById('saved-filters');
  el.innerHTML = '';
  (settings.savedFilters ?? []).forEach((f, i) => {
    const row = document.createElement('div');
    row.className = 'd-flex align-items-center gap-2 mb-1';
    row.innerHTML = `<span class="badge bg-info text-dark">${f.name}</span>
      <small class="text-muted">${[f.level, f.category, f.type, f.search].filter(Boolean).join(', ')}</small>
      <button class="btn btn-sm btn-outline-danger ms-auto py-0" data-idx="${i}">✕</button>`;
    row.querySelector('button').addEventListener('click', () => {
      settings.savedFilters.splice(i, 1);
      renderSavedFilters();
    });
    el.appendChild(row);
  });
}

function addFilter() {
  const name = prompt('Название фильтра:');
  if (!name) return;
  settings.savedFilters.push({ name, level: null, category: null, type: null, search: null });
  renderSavedFilters();
}

function renderRecentPaths() {
  const el = document.getElementById('recent-paths');
  el.innerHTML = '';
  (settings.recentPaths ?? []).forEach((p, i) => {
    const row = document.createElement('div');
    row.className = 'd-flex align-items-center gap-2 mb-1';
    row.innerHTML = `<small class="text-truncate">${p}</small>
      <button class="btn btn-sm btn-outline-danger ms-auto py-0" data-idx="${i}">✕</button>`;
    row.querySelector('button').addEventListener('click', () => {
      settings.recentPaths.splice(i, 1);
      renderRecentPaths();
    });
    el.appendChild(row);
  });
}

async function save() {
  reorderColumns();
  settings.livePollingIntervalSec = Number(document.getElementById('interval-input').value) || 2;
  await saveSettings(settings);
  alert('Настройки сохранены');
}

init();
```

- [ ] **Step 2: Запустить приложение и проверить в браузере**

```powershell
dotnet run --project src/Stoloto.LogParser.Web
```

Открыть `https://localhost:5001` (или порт из вывода). Проверить:
- Кнопка "Выбрать папку" → указать `C:\Frame\docs\lmfs_14_05\lmfs\lmfs_web\Lmfs.Api\bin\Debug\net8.0\logs`
- Нажать "Загрузить" → таблица с записями
- Раскрыть строку кликом → карточка деталей
- Кликнуть `[→ trace]` → trace.html с waterfall
- Открыть `/settings.html` → управление колонками

- [ ] **Step 3: Commit**

```powershell
git add src/Stoloto.LogParser.Web/wwwroot/js/settings.js
git commit -m "feat: add settings.js column drag-drop and saved filters"
```

- [ ] **Step 4: Финальный commit MVP 1**

```powershell
git tag mvp1-local-reader
git log --oneline -20
```

---

## Self-Review

**Покрытие спецификации:**

| Требование | Задача |
|---|---|
| Чтение логов с локального диска (папка / файл) | Task 7, 9, 11 |
| Парсинг 3 форматов из одного файла | Task 3, 4, 5, 6 |
| Фильтры: level, category, type, uid, url, search, дата | Task 9, 14 |
| Управление колонками (видимость, порядок) | Task 14, 16 |
| Раскрытие деталей строки | Task 14 |
| Trace timeline по uid | Task 15 |
| Live-режим с паузой | Task 10, 14 |
| Нераспознанные строки → кнопка → модальное окно | Task 11, 14 |
| Сохранение настроек между сессиями | Task 8, 11 |
| Выбор папки/файла через кнопку с историей | Task 12, 14 |
| Сохранённые фильтры | Task 14, 16 |
| Удаление старых логов — закомментировано | не реализуется в MVP1 |
