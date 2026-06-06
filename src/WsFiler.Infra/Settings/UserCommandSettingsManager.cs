using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WsFiler.Infra.Settings;

public static class UserCommandSettingsManager
{
    private const string FileName = "user-commands.json";

    public static string GetSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "yoshy3", "wsfiler");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
    }

    public static List<UserCommandEntry> Load() => Load(GetSettingsPath());

    public static List<UserCommandEntry> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.UserCommandSettings);
            return settings?.Commands ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<UserCommandEntry> commands) =>
        Save(GetSettingsPath(), commands);

    public static void Save(string path, IEnumerable<UserCommandEntry> commands)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new UserCommandSettings { Commands = [.. commands] };
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.UserCommandSettings);
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }
}
