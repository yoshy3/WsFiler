using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Files;
using WsFiler.Presentation.Resources;

namespace WsFiler.Presentation.ViewModels;

public enum PaneSortField { Name, Extension, Date, Size, Attributes, None }

public sealed partial class FilePaneViewModel : ViewModelBase
{
    public PaneSortField SortField { get; private set; } = PaneSortField.Name;
    public bool SortAscending { get; private set; } = true;
    public string? FilterPattern { get; private set; }

    [ObservableProperty]
    private string currentPath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private bool isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private FileItemViewModel? selectedItem;

    private int cursorIndex;

    public ObservableCollection<FileItemViewModel> Items { get; } = [];

    public string Summary => string.Format(Strings.Pane_Summary, Items.Count, MarkedCount);

    public int MarkedCount => Items.Count(item => item.IsMarked);

    public string PaneInfo => $"Marked {MarkedCount:N0}/{Items.Count:N0} {FormatByteSize(MarkedSize)}";

    public string FreeSpaceInfo => FormatFreeSpace(CurrentPath);

    private long MarkedSize => Items.Where(item => item.IsMarked && !item.IsDirectory).Sum(item => item.RawSize);

    public void Load(string path, IEnumerable<FileSystemItem> items)
    {
        CurrentPath = path;
        Items.Clear();
        cursorIndex = 0;

        var sorted = ApplySortAndFilter(items);
        foreach (var item in sorted)
        {
            Items.Add(new FileItemViewModel(item));
        }

        UpdateSelectedItem();
        OnPaneInfoChanged();
    }

    public void SetSort(PaneSortField field, bool ascending)
    {
        SortField = field;
        SortAscending = ascending;
    }

    public void ApplyFilter(string? pattern)
    {
        FilterPattern = string.IsNullOrWhiteSpace(pattern) ? null : pattern.Trim();
    }

    private IEnumerable<FileSystemItem> ApplySortAndFilter(IEnumerable<FileSystemItem> items)
    {
        IEnumerable<FileSystemItem> result = items;

        if (FilterPattern is not null)
        {
            var regex = WildcardToRegex(FilterPattern);
            result = result.Where(item =>
                item.IsDirectory || regex.IsMatch(item.Name));
        }

        result = SortField switch
        {
            PaneSortField.Extension => SortAscending
                ? result.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : result.OrderByDescending(i => i.IsDirectory).ThenByDescending(i => i.Extension, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            PaneSortField.Date => SortAscending
                ? result.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.ModifiedAt)
                : result.OrderByDescending(i => i.IsDirectory).ThenByDescending(i => i.ModifiedAt),
            PaneSortField.Size => SortAscending
                ? result.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Size ?? 0)
                : result.OrderByDescending(i => i.IsDirectory).ThenByDescending(i => i.Size ?? 0),
            PaneSortField.Attributes => SortAscending
                ? result.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.IsHidden).ThenBy(i => i.IsReadOnly).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : result.OrderByDescending(i => i.IsDirectory).ThenByDescending(i => i.IsHidden).ThenByDescending(i => i.IsReadOnly).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            PaneSortField.None => result,
            _ => SortAscending
                ? result.OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                : result.OrderByDescending(i => i.IsDirectory).ThenByDescending(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
        };

        return result;
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase);
    }

    public FileItemViewModel? CurrentItem => Items.Count == 0 ? null : Items[cursorIndex];

    public IReadOnlyList<FileItemViewModel> OperationTargets
    {
        get
        {
            var markedItems = Items.Where(item => item.IsMarked).ToList();
            if (markedItems.Count > 0)
            {
                return markedItems;
            }

            return CurrentItem is null ? [] : [CurrentItem];
        }
    }

    public void MoveCursor(int delta)
    {
        if (Items.Count == 0)
        {
            cursorIndex = 0;
            UpdateSelectedItem();
            return;
        }

        cursorIndex = Math.Clamp(cursorIndex + delta, 0, Items.Count - 1);
        UpdateSelectedItem();
    }

    public void MoveCursorTo(FileItemViewModel item)
    {
        var index = Items.IndexOf(item);
        if (index < 0)
        {
            return;
        }

        cursorIndex = index;
        UpdateSelectedItem();
    }

    public void ToggleCurrentSelectionAndMoveNext()
    {
        var current = CurrentItem;
        if (current is null)
        {
            return;
        }

        current.ToggleMark();
        OnPaneInfoChanged();
        MoveCursor(1);
    }

    public void ClearMarks()
    {
        foreach (var item in Items)
        {
            item.ClearMark();
        }

        OnPaneInfoChanged();
    }

    public void MarkAll()
    {
        foreach (var item in Items)
        {
            item.MarkSelected();
        }

        OnPaneInfoChanged();
    }

    partial void OnCurrentPathChanged(string value)
    {
        OnPropertyChanged(nameof(FreeSpaceInfo));
    }

    partial void OnIsActiveChanged(bool value)
    {
        UpdateSelectedItem();
    }

    partial void OnSelectedItemChanged(FileItemViewModel? value)
    {
        if (!IsActive || value is null)
        {
            return;
        }

        var index = Items.IndexOf(value);
        if (index >= 0)
        {
            cursorIndex = index;
        }
    }

    private void UpdateSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsCursor = false;
        }

        if (!IsActive || Items.Count == 0)
        {
            SelectedItem = null;
            return;
        }

        cursorIndex = Math.Clamp(cursorIndex, 0, Items.Count - 1);
        var selected = Items[cursorIndex];
        selected.IsCursor = true;
        SelectedItem = selected;
    }

    private void OnPaneInfoChanged()
    {
        OnPropertyChanged(nameof(MarkedCount));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(PaneInfo));
    }

    private static string FormatFreeSpace(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return "";
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? $"{FormatByteSize(drive.AvailableFreeSpace)} Free" : "";
        }
        catch
        {
            return "";
        }
    }

    private static string FormatByteSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)size;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:N0} {units[unitIndex]}"
            : $"{value:N2} {units[unitIndex]}";
    }
}
