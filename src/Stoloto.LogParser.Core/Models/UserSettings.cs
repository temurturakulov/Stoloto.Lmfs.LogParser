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
