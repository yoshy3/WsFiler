using WsFiler.Core.Files;
using WsFiler.Presentation.Theming;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class FileItemViewModel(FileSystemItem item) : ViewModelBase
{
    public string FullPath { get; } = item.FullPath;

    public string Name { get; } = item.Name;

    public string DisplayName => IsMarked ? $"* {BaseName}" : $"  {BaseName}";

    public string BaseName { get; } = GetBaseName(item);

    public string Extension { get; } = item.Extension;

    public string Size { get; } = item.ItemType == FileSystemItemType.Directory ? "<DIR>" : FormatSize(item.Size);

    public long RawSize { get; } = item.Size ?? 0;

    public string Modified { get; } = item.ModifiedAt == default
        ? ""
        : item.ModifiedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    public string Kind { get; } = item.ItemType switch
    {
        FileSystemItemType.Directory => "D",
        FileSystemItemType.SymbolicLink => "L",
        FileSystemItemType.File => "F",
        _ => "O",
    };

    public string ForegroundColor
    {
        get
        {
            if (UiTheme.IsLight)
            {
                if (IsParent) return "#202020";
                if (IsHidden) return "#909090";
                if (IsReadOnly) return "#a05a00";
                if (IsDirectory) return "#0a4ea8";
                return "#202020";
            }

            if (IsParent) return "#f4f4f4";
            if (IsHidden) return "#808080";
            if (IsReadOnly) return "#ffd070";
            if (IsDirectory) return "#6fb7ff";
            return "#f4f4f4";
        }
    }

    public string RowBackground => IsMarked
        ? (UiTheme.IsLight ? "#80AFC8E8" : "#804C709F")
        : "Transparent";

    public void RaiseColorsChanged()
    {
        OnPropertyChanged(nameof(ForegroundColor));
        OnPropertyChanged(nameof(RowBackground));
    }

    public bool IsDirectory { get; } = item.IsDirectory;

    public bool IsHidden { get; } = item.IsHidden;

    public bool IsReadOnly { get; } = item.IsReadOnly;

    public bool IsSystem { get; } = item.IsSystem;

    public bool IsParent => Name == "..";

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
        if (IsParent) return;
        IsMarked = !IsMarked;
        Mark = IsMarked ? "*" : "";
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
        if (IsParent) return;
        IsMarked = true;
        Mark = "*";
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(RowBackground));
    }

    private static string FormatSize(long? size)
    {
        return size is null ? "" : size.Value.ToString("N0");
    }

    private static string GetBaseName(FileSystemItem item)
    {
        if (item.IsDirectory || string.IsNullOrEmpty(item.Extension))
        {
            return item.Name;
        }

        var extensionWithDot = "." + item.Extension;
        if (item.Name.Length <= extensionWithDot.Length ||
            !item.Name.EndsWith(extensionWithDot, StringComparison.OrdinalIgnoreCase))
        {
            return item.Name;
        }

        return item.Name[..^extensionWithDot.Length];
    }
}
