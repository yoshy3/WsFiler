namespace WsFiler.Core.Files;

public sealed record FileDeleteConfirmationInfo(
    string TargetPath,
    string ItemName,
    bool IsDirectory,
    bool IsReadOnly);
