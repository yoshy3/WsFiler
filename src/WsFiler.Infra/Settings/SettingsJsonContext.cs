using System.Text.Json.Serialization;

namespace WsFiler.Infra.Settings;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(WindowSettings))]
[JsonSerializable(typeof(DirectoryHistorySettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
