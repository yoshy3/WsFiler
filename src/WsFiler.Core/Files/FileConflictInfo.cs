namespace WsFiler.Core.Files;

public sealed record FileConflictInfo(
    string SourcePath,
    string DestinationPath,
    string ItemName,
    bool IsDirectory);
