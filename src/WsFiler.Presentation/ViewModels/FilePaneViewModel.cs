using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Files;
using WsFiler.Presentation.Resources;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class FilePaneViewModel : ViewModelBase
{
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

    public void Load(string path, IEnumerable<FileSystemItem> items)
    {
        CurrentPath = path;
        Items.Clear();
        cursorIndex = 0;

        foreach (var item in items)
        {
            Items.Add(new FileItemViewModel(item));
        }

        UpdateSelectedItem();
        OnPropertyChanged(nameof(Summary));
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
        OnPropertyChanged(nameof(MarkedCount));
        OnPropertyChanged(nameof(Summary));
        MoveCursor(1);
    }

    public void ClearMarks()
    {
        foreach (var item in Items)
        {
            item.ClearMark();
        }

        OnPropertyChanged(nameof(MarkedCount));
        OnPropertyChanged(nameof(Summary));
    }

    public void MarkAll()
    {
        foreach (var item in Items)
        {
            item.MarkSelected();
        }

        OnPropertyChanged(nameof(MarkedCount));
        OnPropertyChanged(nameof(Summary));
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
        if (!IsActive || Items.Count == 0)
        {
            SelectedItem = null;
            return;
        }

        cursorIndex = Math.Clamp(cursorIndex, 0, Items.Count - 1);
        SelectedItem = Items[cursorIndex];
    }
}
