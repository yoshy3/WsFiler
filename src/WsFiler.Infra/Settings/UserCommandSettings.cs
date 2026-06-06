using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WsFiler.Infra.Settings;

public sealed class UserCommandSettings
{
    [JsonPropertyName("commands")]
    public List<UserCommandEntry> Commands { get; set; } = [];
}

public sealed class UserCommandEntry
{
    public const string WorkingDirectoryCurrent = "current";
    public const string WorkingDirectoryExecutable = "executable";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("executablePath")]
    public string? ExecutablePath { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }

    [JsonPropertyName("workingDirectoryMode")]
    public string? WorkingDirectoryMode { get; set; } = WorkingDirectoryCurrent;
}
