# Stoloto.LogParser — Спецификация дизайна

**Дата:** 2026-05-25  
**Проект:** Stoloto.LogParser  
**Источник логов:** Lmfs.Api (ASP.NET Core 8, NLog + Stoloto.Framework)

---

## 1. Контекст

Проект Lmfs.Api пишет логи в папку `logs/` через два источника:
- **NLog 5.4** — основной логгер, JsonLayout, UTF-8
- **Stoloto.Framework.Logger** — внешняя библиотека, JSON UTF-16 LE

Все записи попадают в **один файл на день**: `Lmfs.Api.YYYY-MM-DD.log`.  
Внутри файла строки могут быть в трёх форматах вперемешку.

### Три формата строк в одном файле

| Формат | Признак | Пример полей |
|--------|---------|-------------|
| NLog JSON | `{` + lowercase `datetime` | `datetime`, `level`, `logger`, `message`, `uid`, `category`, `type`, `body`, `url`, `responseTime` |
| Stoloto JSON | `{` + uppercase `Date`/`LogLevel` | `Date`, `LogLevel`, `Message`, `HttpRequestUrl`, `HttpResponseStatus` |
| Pipe-delimited | содержит `\|` разделители | `datetime\|level\|logger\|message` |

### Ключевая архитектурная особенность логов

Все записи одного HTTP-запроса связаны через **uid** (Guid). Это встроенный distributed tracing:
```
uid=cae313b9 → api.request → auth.request → auth.response → kkt.request → kkt.response → db.request → db.response → api.response
```

### Категории (LogCategory)

`api` | `kkt` | `pos` | `db` | `auth` | `schedule` | `queue` | `file`

### Типы (LogType)

`request` | `response`

---

## 2. Цели и MVP

### MVP 1 — Local Reader (реализуем сейчас)
Читает лог-файлы напрямую с локального диска. Никаких агентов, никакой БД.

### MVP 2 — Distributed (следующий этап)
HTTP-агенты на каждом ПК, центральный сервер, DuckDB, авто-обнаружение ПК.

---

## 3. Архитектура решения

### Структура .NET solution

```
Stoloto.LogParser.sln
├── src/
│   ├── Stoloto.LogParser.Web/       ← ASP.NET Core 8, контроллеры, UI
│   ├── Stoloto.LogParser.Core/      ← парсинг, модели, фильтрация (shared)
│   ├── Stoloto.LogParser.Storage/   ← DuckDB, репозитории (MVP 2)
│   └── Stoloto.LogParser.Agent/     ← .NET Worker Service агент (MVP 2)
└── docs/
```

`Core` используется и в `Web` и в `Agent` — парсинг пишется один раз.

### Технологический стек

| Слой | Технология |
|------|-----------|
| Backend | ASP.NET Core 8.0 minimal API |
| Frontend | Bootstrap 5.3 + vanilla JS (ES modules) |
| БД (MVP 2) | DuckDB.NET |
| Агент (MVP 2) | .NET 8 Worker Service (Windows Service) |
| Настройки | System.Text.Json + appsettings.user.json |
| Live | SSE (Server-Sent Events) |
| Парсинг JSON | System.Text.Json |

Никаких npm, webpack, node_modules. `dotnet run` — всё работает.

---

## 4. Единая модель данных

Все три формата нормализуются в одну модель:

```csharp
public class LogEntry
{
    public DateTime Datetime { get; set; }
    public string Level { get; set; }           // Info, Warn, Error
    public string Logger { get; set; }
    public string Message { get; set; }
    public string? Uid { get; set; }
    public string? Category { get; set; }       // api, kkt, pos, db, auth...
    public string? Type { get; set; }           // request, response
    public string? Url { get; set; }
    public string? Body { get; set; }
    public decimal? ResponseTime { get; set; }
    public int? HttpCode { get; set; }
    public string? Details { get; set; }
    public Dictionary<string, object> Extra { get; set; }  // все нестандартные поля
    public string SourceFile { get; set; }      // из какого файла
    public string SourcePc { get; set; }        // с какого ПК (MVP 2)
}
```

`Extra` хранит все нестандартные поля (`commandText`, `EventId`, `elapsed` и т.д.) — это позволяет отображать любые поля в UI без изменения модели.

---

## 5. Парсинг

### Интерфейс

```csharp
public interface ILogParser
{
    bool CanParse(string line);
    LogEntry? ParseLine(string line);
}
```

### Логика определения формата (построчно)

```
строка → starts with { ?
    да → has lowercase "datetime" → NLogJsonParser
         has uppercase "Date"     → StolotoJsonParser
    нет → contains | separators  → PipeDelimitedParser
          иначе                  → пропустить, добавить в skippedLines
```

`ParserFactory` работает на уровне строки, не файла. Один проход — все три формата обработаны.

### Нераспознанные строки

Строки, не распознанные ни одним парсером, сохраняются в буфер `skippedLines` с номером строки в файле. В UI показывается кликабельная кнопка `⚠ Пропущено N строк` — открывает модальное окно со списком строк. Клик по строке показывает её полный текст.

---

## 6. Структура папок проекта

```
Stoloto.LogParser.Web/
├── Controllers/
│   ├── LogsController.cs
│   ├── SettingsController.cs
│   └── AgentsController.cs        (MVP 2)
├── Services/
│   ├── LogQueryService.cs
│   ├── LiveLogService.cs
│   └── SettingsService.cs
├── wwwroot/
│   ├── index.html
│   ├── css/
│   └── js/
│       ├── app.js                 ← главная страница
│       ├── trace.js               ← trace timeline
│       ├── settings.js            ← настройки
│       └── api.js                 ← fetch-обёртки
└── Program.cs

Stoloto.LogParser.Core/
├── Models/
│   ├── LogEntry.cs
│   ├── UserSettings.cs
│   └── AgentInfo.cs               (MVP 2)
├── Parsers/
│   ├── ILogParser.cs
│   ├── LogParserFactory.cs
│   ├── NLogJsonParser.cs
│   ├── StolotoJsonParser.cs
│   └── PipeDelimitedParser.cs
└── Sources/
    ├── LocalLogSource.cs
    └── DuckDbLogSource.cs         (MVP 2)

Stoloto.LogParser.Agent/           (MVP 2)
├── Program.cs
├── FileWatcherService.cs
└── AgentRegistrationService.cs
```

---

## 7. API эндпоинты

### MVP 1

```
GET  /api/logs
     ?path=C:\...\logs
     &dateFrom=2026-05-13
     &dateTo=2026-05-13
     &levels=Error,Warn
     &categories=kkt,api
     &type=response
     &uid=cae313b9
     &search=FiscalCheck
     &page=1
     &pageSize=100
     → { items: LogEntry[], total: int, page: int, skippedLines: SkippedLine[] }

GET  /api/logs/trace/:uid
     ?path=C:\...\logs&date=2026-05-13
     → { entries: LogEntry[], totalDuration: decimal }

GET  /api/logs/live
     ?path=C:\...\logs&levels=Error&categories=kkt
     → SSE stream (text/event-stream)
     → data: { entries: LogEntry[] }  каждые 2 сек

GET  /api/logs/fields
     ?path=C:\...\logs&date=2026-05-13
     → { fields: string[] }  все поля встреченные в выборке

GET  /api/settings
     → UserSettings

PUT  /api/settings
     body: UserSettings
     → UserSettings
```

### MVP 2 (добавляются)

```
POST /api/agents/register
     body: { hostname, ip, logsPath, osVersion, agentVersion }
     → { agentId: guid }

GET  /api/agents
     → { agents: AgentInfo[] }

POST /api/ingest
     body: { agentId, entries: LogEntry[] }
     → { accepted: int }

POST /api/agents/heartbeat
     body: { agentId }
```

---

## 8. Поток данных MVP 1

```
Пользователь задаёт фильтры
        │
LogsController.Get(query)
        │
LogQueryService.QueryAsync(query)
        ├── определяет файлы по dateFrom/dateTo
        ├── LocalLogSource.ReadAsync() → IAsyncEnumerable<string>
        │       └── построчно → LogParserFactory.ParseLine(line)
        ├── фильтрация в памяти
        ├── сортировка по datetime DESC
        └── пагинация → вернуть page*pageSize + total
```

Файлы читаются **лениво через IAsyncEnumerable** — не грузим весь файл в память.

### Live-режим MVP 1

```
LiveLogService (IHostedService)
        ├── запоминает byte offset последней прочитанной позиции
        ├── каждые 2 сек: читает новые байты с offset до конца
        │       └── парсит → фильтрует → пушит в SSE
        └── при смене дня — переключается на новый файл
```

---

## 9. MVP 2 — Агент

```
Старт агента
        ├── POST /api/agents/register → получает agentId
        ├── FileWatcherService
        │     ├── FileSystemWatcher следит за папкой logs
        │     ├── буферизует пачками 50 записей или каждые 5 сек
        │     └── POST /api/ingest { agentId, entries }
        └── Heartbeat каждые 30 сек → POST /api/agents/heartbeat

Авторегистрация передаёт:
    hostname, ip, osVersion, agentVersion, logsPath
```

При старте агент отправляет все метаданные ПК автоматически. Пользователь может добавить понятное имя ("Касса №3, Ленина 12") в UI.

---

## 10. DuckDB схема (MVP 2)

```sql
CREATE TABLE log_entries (
    id            BIGINT PRIMARY KEY,
    datetime      TIMESTAMP NOT NULL,
    level         VARCHAR,
    logger        VARCHAR,
    message       VARCHAR,
    uid           VARCHAR,
    category      VARCHAR,
    type          VARCHAR,
    url           VARCHAR,
    body          VARCHAR,
    response_time DOUBLE,
    http_code     INTEGER,
    source_file   VARCHAR,
    source_pc     VARCHAR,
    extra         JSON
);

CREATE INDEX idx_datetime ON log_entries(datetime);
CREATE INDEX idx_uid      ON log_entries(uid);
CREATE INDEX idx_level    ON log_entries(level);
CREATE INDEX idx_category ON log_entries(category);
```

Фоновая очистка старых записей — `IHostedService` раз в сутки в 03:00 (закомментировано до явного включения).

---

## 11. UI — главная страница

### Layout

```
┌─────────────────────────────────────────────────────────────┐
│  LMFS Log Parser    [📁 C:\Frame\...\logs ▼]   [live ●]     │
├─────────────────────────────────────────────────────────────┤
│  [Дата: 2026-05-13] [Уровень ▼] [Категория ▼] [Тип ▼]      │
│  [UID: ___________] [URL: _______] [Поиск: __________]      │
│  [level: Error ✕] [category: kkt ✕]    Сбросить все         │
│  [Только ошибки] [KKT запросы] [API 500] [+ сохранить]      │
├──────────┬──────────────────────────────────────────────────┤
│ Колонки  │  datetime  level  category  type  url  message   │
│ [✓] dt   ├──────────────────────────────────────────────────┤
│ [✓] lvl  │  10:00:46  Info   api    request  /check   ...   │
│ [✓] cat  │  10:00:46  Info   auth   request  Login    ...   │
│ [✓] type │  10:00:47  Error  kkt    response FiscalCh ...   │
│ [ ] uid  │  10:00:48  Info   db     request  SaveRec  ...   │
│ [ ] body ├──────────────────────────────────────────────────┤
│          │  [← 1  2  3  4  5 →]   Всего: 1 842              │
└──────────┴──────────────────────────────────────────────────┘
⚠ Пропущено 12 строк  ← кнопка
```

### Раскрытие строки (клик)

```
▼  10:00:47  Error  kkt  response  FiscalCheck  Ошибка фискализации
   ┌──────────────────────────────────────────────────────────┐
   │ datetime:     2026-05-13 10:00:47.8821                   │
   │ level:        Error                                      │
   │ category:     kkt                                        │
   │ type:         response                                   │
   │ url:          FiscalCheck                                │
   │ uid:          cae313b9-a222-48b2-...     [→ trace]       │
   │ body:         { "error": "Нет связи с ККТ" }             │
   │ responseTime: 5.234s                                     │
   │ source:       Lmfs.Api.2026-05-13.log                   │
   └──────────────────────────────────────────────────────────┘
```

### Выбор источника логов (модальное окно)

```
┌─────────────────────────────────────────────────────────────┐
│  Источник логов                                        [✕]  │
├─────────────────────────────────────────────────────────────┤
│  Режим:  (●) Папка    ( ) Файл                              │
│  Путь:  [C:\Frame\...\logs________________]  [Обзор...]     │
│  Последние:                                                 │
│  ▸ C:\Frame\docs\lmfs_14_05\...\logs                        │
│  Файлы найдены: Lmfs.Api.2026-05-13.log (+12 файлов)        │
│                          [Отмена]        [Применить]        │
└─────────────────────────────────────────────────────────────┘
```

`[Обзор...]` — File System Access API (`showDirectoryPicker` / `showOpenFilePicker`). Fallback — ввод пути вручную.

---

## 12. UI — Trace Timeline (по uid)

```
uid: cae313b9  |  /api/check  |  200 OK  |  total: 1.234s

0ms     200ms    400ms    600ms    800ms   1000ms   1200ms
[api    ]████████████████████████████████████████████  1234ms
         [auth  ]███                                     43ms
         [db    ]    █████                              120ms
                 [kkt    ]████████████████              800ms
                                         [db    ]████    98ms
```

Каждая полоска кликабельна — разворачивается в карточку с полными деталями записи.

Цвета по категориям: `api`=синий, `kkt`=оранжевый, `db`=серый, `auth`=зелёный, `pos`=фиолетовый.

---

## 13. UI — Настройки (/settings)

- Управление колонками: видимость + порядок через drag-and-drop
- Интервал live-polling (по умолчанию 2 сек)
- Сохранённые фильтры: создание, удаление
- Последние пути: список с кнопкой удаления
- Кнопка "Сбросить всё"

Настройки сохраняются в `appsettings.user.json` при каждом изменении.

---

## 14. Настройки — модель

```csharp
public class UserSettings
{
    public string? LastLogPath { get; set; }
    public List<string> RecentPaths { get; set; } = new();
    public int LivePollingIntervalSec { get; set; } = 2;
    public List<ColumnSetting> Columns { get; set; } = DefaultColumns();
    public List<SavedFilter> SavedFilters { get; set; } = new();
}

public class ColumnSetting
{
    public string Name { get; set; }
    public bool Visible { get; set; }
    public int Order { get; set; }
}

public class SavedFilter
{
    public string Name { get; set; }
    public string? Level { get; set; }
    public string? Category { get; set; }
    public string? Type { get; set; }
    public string? Search { get; set; }
}
```

---

## 15. Обработка ошибок

| Ситуация | Поведение |
|----------|-----------|
| Строка не распознана парсером | Добавляется в `skippedLines` с номером строки в файле |
| Путь не существует / нет прав | HTTP 400 + понятное сообщение в UI |
| Файл > 50MB | IAsyncEnumerable с ранней остановкой, никогда не в память целиком |
| Файл заблокирован NLog при записи | Открываем с `FileShare.ReadWrite` |
| SSE соединение оборвалось | JS клиент переподключается автоматически через 3 сек |

---

## 16. Границы скоупа

### MVP 1 (реализуем сейчас)
- Чтение логов с локального диска (папка или конкретный файл)
- Парсинг всех трёх форматов в едином файле
- Таблица с фильтрами: level, category, type, uid, url, search, диапазон дат
- Управление колонками (видимость, порядок drag-and-drop)
- Раскрытие деталей записи
- Trace timeline по uid
- Live-режим через polling (SSE)
- Просмотр нераспознанных строк
- Сохранение настроек
- Выбор папки/файла через кнопку с историей путей
- Сохранённые фильтры

### MVP 2 (следующий этап)
- HTTP-агент (.NET Worker Service / Windows Service)
- Авторегистрация агента: hostname, ip, osVersion, logsPath
- Push логов с агента в центральный сервер
- DuckDB как хранилище
- Список агентов с онлайн/оффлайн статусом
- Переключение источника: локальный / агент
- Live через SSE вместо polling

### Вне скоупа намеренно
- Удаление старых логов (код закомментирован, включается явно позже)
- Авторизация и аутентификация
- Алёрты и уведомления
- Экспорт в CSV/Excel
