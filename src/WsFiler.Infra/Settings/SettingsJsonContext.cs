using System.Text.Json.Serialization;

namespace WsFiler.Infra.Settings;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(UpdateCheckSettings))]
[JsonSerializable(typeof(WindowSettings))]
[JsonSerializable(typeof(DirectoryHistorySettings))]
[JsonSerializable(typeof(UserCommandSettings))]
[JsonSerializable(typeof(UserCommandEntry))]
public partial class SettingsJsonContext : JsonSerializerContext
{
}
