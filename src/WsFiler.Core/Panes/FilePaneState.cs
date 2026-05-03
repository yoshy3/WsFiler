using WsFiler.Core.Files;

namespace WsFiler.Core.Panes;

public sealed class FilePaneState
{
    public string CurrentPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public List<FileSystemItem> Items { get; } = [];

    public int CursorIndex { get; set; }

    public HashSet<string> SelectedPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void ClearForNavigation(string path, IEnumerable<FileSystemItem> items)
    {
        CurrentPath = path;
        Items.Clear();
        Items.AddRange(items);
        CursorIndex = Items.Count == 0 ? -1 : 0;
        SelectedPaths.Clear();
    }
}
