using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WsFiler.Infra.Settings;

public static class DirectoryHistoryManager
{
    private const string HistoryFileName = "directory-history.json";

    public static string GetHistoryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "yoshy3", "wsfiler");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, HistoryFileName);
    }

    public static IReadOnlyList<string> Load()
    {
        return Load(GetHistoryPath());
    }

    public static IReadOnlyList<string> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize(
                json,
                SettingsJsonContext.Default.DirectoryHistorySettings);
            return settings?.Paths ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<string> paths)
    {
        Save(GetHistoryPath(), paths);
    }

    public static void Save(string path, IEnumerable<string> paths)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var settings = new DirectoryHistorySettings
            {
                Paths = [.. paths],
            };
            var json = JsonSerializer.Serialize(
                settings,
                SettingsJsonContext.Default.DirectoryHistorySettings);
            File.WriteAllText(path, json);
        }
        catch
        {
        }
    }
}
