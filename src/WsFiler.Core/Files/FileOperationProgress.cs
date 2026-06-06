namespace WsFiler.Core.Files;

public sealed record FileOperationProgress(
    string CurrentPath,
    int CompletedItems,
    int? TotalItems);
