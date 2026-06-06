namespace WsFiler.Core.Commands;

public sealed record UserCommandDefinition(
    string Name,
    string ExecutablePath,
    string Arguments)
{
    public const string CommandIdPrefix = "user.";

    public string CommandId => ToCommandId(Name);

    public static string ToCommandId(string name) => $"{CommandIdPrefix}{name}";

    public static bool IsUserCommandId(string commandId) =>
        commandId.StartsWith(CommandIdPrefix, StringComparison.OrdinalIgnoreCase);

    public static string GetNameFromCommandId(string commandId) =>
        IsUserCommandId(commandId) ? commandId[CommandIdPrefix.Length..] : commandId;
}
