using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WsFiler.Infra.Settings;

public sealed class AppSettings
{
    [JsonPropertyName("lastSession")]
    public LastSessionSettings? LastSession { get; set; }

    [JsonPropertyName("keyMap")]
    public Dictionary<string, string>? KeyMap { get; set; }

    [JsonPropertyName("theme")]
    public string? Theme { get; set; } = "system";

    [JsonPropertyName("language")]
    public string? Language { get; set; } = "system";

    [JsonPropertyName("externalEditor")]
    public string? ExternalEditor { get; set; }

    [JsonPropertyName("directoryBookmarks")]
    public List<string>? DirectoryBookmarks { get; set; }

    [JsonPropertyName("updateCheck")]
    public UpdateCheckSettings? UpdateCheck { get; set; } = new();

    [JsonPropertyName("window")]
    public WindowSettings? Window { get; set; }
}

public sealed class UpdateCheckSettings
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("ignoredVersion")]
    public string? IgnoredVersion { get; set; }
}

public sealed class LastSessionSettings
{
    [JsonPropertyName("leftPath")]
    public string? LeftPath { get; set; }

    [JsonPropertyName("rightPath")]
    public string? RightPath { get; set; }

    [JsonPropertyName("leftSortField")]
    public string? LeftSortField { get; set; }

    [JsonPropertyName("leftSortAscending")]
    public bool LeftSortAscending { get; set; } = true;

    [JsonPropertyName("rightSortField")]
    public string? RightSortField { get; set; }

    [JsonPropertyName("rightSortAscending")]
    public bool RightSortAscending { get; set; } = true;
}

public sealed class WindowSettings
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("isMaximized")]
    public bool IsMaximized { get; set; }
}
