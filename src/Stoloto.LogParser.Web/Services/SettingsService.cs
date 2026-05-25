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
