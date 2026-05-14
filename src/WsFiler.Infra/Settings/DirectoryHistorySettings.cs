using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WsFiler.Infra.Settings;

public sealed class DirectoryHistorySettings
{
    [JsonPropertyName("paths")]
    public List<string>? Paths { get; set; }
}
