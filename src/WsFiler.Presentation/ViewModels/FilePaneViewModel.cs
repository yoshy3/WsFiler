using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Files;
using WsFiler.Presentation.Resources;
using WsFiler.Presentation.Theming;

namespace WsFiler.Presentation.ViewModels;

public enum PaneSortField { Name, Extension, Date, Size, Attributes, None }

public sealed partial class FilePaneViewModel : ViewModelBase
{
    public PaneSortField SortField { get; private set; } = PaneSortField.Name;
    public bool SortAscending { get; private set; } = true;
    public string? FilterPattern { get; private set; }
    public bool ShowHiddenFiles { get; private set; }

    private readonly Dictionary<string, string> cursorMemory = new(StringComparer.OrdinalIgnoreCase);

    public FilePaneViewModel()
    {
        UiTheme.Changed += OnUiThemeChanged;
    }

    private void OnUiThemeChanged()
    {
        foreach (var item in Items)
        {
            item.RaiseColorsChanged();
        }
    }

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
        SaveCursorMemory();

        CurrentPath = path;
        Items.Clear();
        cursorIndex = 0;

        if (HasParentDirectory(path))
        {
            var parent = new FileSystemItem(
                "..",
                path,
                FileSystemItemType.Directory,
                null,
                default,
                "",
                false,
                false);
            Items.Add(new FileItemViewModel(parent));
        }

        var sorted = ApplySortAndFilter(items);
        foreach (var item in sorted)
        {
            Items.Add(new FileItemViewModel(item));
        }

        RestoreCursorFromMemory(path);
        UpdateSelectedItem();
        OnPaneInfoChanged();
    }

    public void RememberCursorForPath(string path, string itemName)
    {
        if (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(itemName))
        {
            cursorMemory[path] = itemName;
        }
    }

    private void SaveCursorMemory()
    {
        if (string.IsNullOrEmpty(CurrentPath) || Items.Count == 0)
        {
            return;
        }

        if (cursorIndex < 0 || cursorIndex >= Items.Count)
        {
            return;
        }

        var name = Items[cursorIndex].Name;
        if (name == "..")
        {
            return;
        }

        cursorMemory[CurrentPath] = name;
    }

    private void RestoreCursorFromMemory(string path)
    {
        if (!cursorMemory.TryGetValue(path, out var name))
        {
            return;
        }

        for (var i = 0; i < Items.Count; i++)
        {
            if (string.Equals(Items[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                cursorIndex = i;
                return;
            }
        }
    }

    private static bool HasParentDirectory(string path)
    {
        try
        {
            return Directory.GetParent(path) is not null;
        }
        catch
        {
            return false;
        }
    }

    public void SetSort(PaneSortField field, bool ascending)
    {
        SortField = field;
        SortAscending = ascending;
    }

    public void ApplyFilter(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            FilterPattern = null;
            return;
        }

        var trimmed = pattern.Trim();
        if (!trimmed.Contains('*') && !trimmed.Contains('?'))
        {
            trimmed = $"*{trimmed}*";
        }

        FilterPattern = trimmed;
    }

    public void SetShowHiddenFiles(bool value)
    {
        ShowHiddenFiles = value;
    }

    private IEnumerable<FileSystemItem> ApplySortAndFilter(IEnumerable<FileSystemItem> items)
    {
        IEnumerable<FileSystemItem> result = items;

        if (!ShowHiddenFiles)
        {
            result = result.Where(item => !item.IsHidden);
        }

        if (FilterPattern is not null)
        {
            var regex = WildcardToRegex(FilterPattern);
            result = result.Where(item => regex.IsMatch(item.Name));
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
            var markedItems = Items.Where(item => item.IsMarked && !item.IsParent).ToList();
            if (markedItems.Count > 0)
            {
                return markedItems;
            }

            return CurrentItem is null || CurrentItem.IsParent ? [] : [CurrentItem];
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

    public void MoveCursorPage(int pageSize, int direction)
    {
        MoveCursor(Math.Max(1, pageSize) * direction);
    }

    public void MoveCursorFirst()
    {
        cursorIndex = 0;
        UpdateSelectedItem();
    }

    public void MoveCursorLast()
    {
        cursorIndex = Math.Max(0, Items.Count - 1);
        UpdateSelectedItem();
    }

    public bool MoveCursorToFirstNameMatch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var index = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i].Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index >= Items.Count)
        {
            return false;
        }

        cursorIndex = index;
        UpdateSelectedItem();
        return true;
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

    public void MoveCursorToPath(string fullPath)
    {
        var index = -1;
        for (var i = 0; i < Items.Count; i++)
        {
            if (string.Equals(Items[i].FullPath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

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
