using System;
using System.IO;
using System.Text.Json;

namespace WsFiler.Infra.Settings;

public static class SettingsManager
{
    private const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "yoshy3", "wsfiler");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, SettingsFileName);
    }

    public static AppSettings Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path)) return new AppSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch { }
    }
}
