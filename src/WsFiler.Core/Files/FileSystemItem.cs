namespace WsFiler.Core.Files;

public sealed record FileSystemItem(
    string Name,
    string FullPath,
    FileSystemItemType ItemType,
    long? Size,
    DateTimeOffset ModifiedAt,
    string Extension,
    bool IsHidden,
    bool IsReadOnly)
{
    public bool IsDirectory => ItemType == FileSystemItemType.Directory;
}
