using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Files;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class FilePaneViewModel : ViewModelBase
{
    [ObservableProperty]
    private string currentPath = "";

    [ObservableProperty]
    private bool isActive;

    public ObservableCollection<FileItemViewModel> Items { get; } = [];

    public void Load(string path, IEnumerable<FileSystemItem> items)
    {
        CurrentPath = path;
        Items.Clear();

        foreach (var item in items)
        {
            Items.Add(new FileItemViewModel(item));
        }
    }
}
