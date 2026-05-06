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
}

public sealed class LastSessionSettings
{
    [JsonPropertyName("leftPath")]
    public string? LeftPath { get; set; }

    [JsonPropertyName("rightPath")]
    public string? RightPath { get; set; }
}
