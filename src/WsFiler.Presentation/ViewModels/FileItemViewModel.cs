using WsFiler.Core.Files;

namespace WsFiler.Presentation.ViewModels;

public sealed class FileItemViewModel(FileSystemItem item)
{
    public string Name { get; } = item.Name;

    public string Extension { get; } = item.Extension;

    public string Size { get; } = item.ItemType == FileSystemItemType.Directory ? "<DIR>" : FormatSize(item.Size);

    public string Modified { get; } = item.ModifiedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public string Kind { get; } = item.ItemType switch
    {
        FileSystemItemType.Directory => "D",
        FileSystemItemType.SymbolicLink => "L",
        FileSystemItemType.File => "F",
        _ => "O",
    };

    private static string FormatSize(long? size)
    {
        return size is null ? "" : size.Value.ToString("N0");
    }
}
