using WsFiler.Core.Files;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class FileItemViewModel(FileSystemItem item) : ViewModelBase
{
    public string FullPath { get; } = item.FullPath;

    public string Name { get; } = item.Name;

    public string DisplayName => IsMarked ? $"> {Name}" : $"  {Name}";

    public string Extension { get; } = item.Extension;

    public string Size { get; } = item.ItemType == FileSystemItemType.Directory ? "<DIR>" : FormatSize(item.Size);

    public long RawSize { get; } = item.Size ?? 0;

    public string Modified { get; } = item.ModifiedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public string Kind { get; } = item.ItemType switch
    {
        FileSystemItemType.Directory => "D",
        FileSystemItemType.SymbolicLink => "L",
        FileSystemItemType.File => "F",
        _ => "O",
    };

    public string ForegroundColor => IsDirectory ? "#6fb7ff" : "#f4f4f4";

    public string RowBackground => IsMarked ? "#804C709F" : "Transparent";

    public bool IsDirectory { get; } = item.IsDirectory;

    private bool isMarked;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool isCursor;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string mark = "";

    public bool IsMarked
    {
        get => isMarked;
        private set => SetProperty(ref isMarked, value);
    }

    public void ToggleMark()
    {
        IsMarked = !IsMarked;
        Mark = IsMarked ? ">" : "";
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RowBackground));
    }

    public void ClearMark()
    {
        IsMarked = false;
        Mark = "";
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RowBackground));
    }

    public void MarkSelected()
    {
        IsMarked = true;
        Mark = ">";
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RowBackground));
    }

    private static string FormatSize(long? size)
    {
        return size is null ? "" : size.Value.ToString("N0");
    }
}
