using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Files;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileSystemProvider fileSystemProvider;

    [ObservableProperty]
    private string statusMessage = "Ready";

    public string StatusSummary => $"{LeftPane.Summary} | {RightPane.Summary}";

    public FilePaneViewModel LeftPane { get; } = new() { IsActive = true };

    public FilePaneViewModel RightPane { get; } = new();

    public MainWindowViewModel(IFileSystemProvider? fileSystemProvider = null)
    {
        this.fileSystemProvider = fileSystemProvider ?? new EmptyFileSystemProvider();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LoadInitialPanes(this.fileSystemProvider, home);
    }

    public async Task HandleKeyAsync(string key)
    {
        switch (key)
        {
            case "Up":
                ActivePane.MoveCursor(-1);
                break;
            case "Down":
                ActivePane.MoveCursor(1);
                break;
            case "Tab":
                SwitchActivePane();
                break;
            case "Left":
                await HandleHorizontalAsync(PaneDirection.Left);
                break;
            case "Right":
                await HandleHorizontalAsync(PaneDirection.Right);
                break;
            case "Enter":
                await OpenCurrentDirectoryAsync();
                break;
            case "Back":
                await NavigateParentAsync();
                break;
            case "Space":
                ActivePane.ToggleCurrentSelectionAndMoveNext();
                OnPropertyChanged(nameof(StatusSummary));
                break;
        }
    }

    public FilePaneViewModel ActivePane => LeftPane.IsActive ? LeftPane : RightPane;

    private void SwitchActivePane()
    {
        LeftPane.IsActive = !LeftPane.IsActive;
        RightPane.IsActive = !RightPane.IsActive;
    }

    private async Task HandleHorizontalAsync(PaneDirection direction)
    {
        var isActiveLeft = LeftPane.IsActive;
        var outward = isActiveLeft ? direction == PaneDirection.Left : direction == PaneDirection.Right;

        if (outward)
        {
            await NavigateParentAsync();
            return;
        }

        SwitchActivePane();
    }

    private async Task OpenCurrentDirectoryAsync()
    {
        var current = ActivePane.CurrentItem;
        if (current is null)
        {
            return;
        }

        if (!current.IsDirectory)
        {
            StatusMessage = "Preview is not available yet";
            return;
        }

        await LoadPaneAsync(ActivePane, current.FullPath);
    }

    private async Task NavigateParentAsync()
    {
        var parent = Directory.GetParent(ActivePane.CurrentPath);
        if (parent is null)
        {
            StatusMessage = "Already at root";
            return;
        }

        await LoadPaneAsync(ActivePane, parent.FullName);
    }

    private async Task LoadPaneAsync(FilePaneViewModel pane, string path)
    {
        try
        {
            var items = await fileSystemProvider.ListDirectoryAsync(path);
            pane.Load(path, items);
            StatusMessage = path;
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async void LoadInitialPanes(IFileSystemProvider fileSystemProvider, string home)
    {
        try
        {
            var items = await fileSystemProvider.ListDirectoryAsync(home);
            LeftPane.Load(home, items);
            RightPane.Load(home, items);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            LeftPane.Load(home, []);
            RightPane.Load(home, []);
            OnPropertyChanged(nameof(StatusSummary));
        }
    }

    private sealed class EmptyFileSystemProvider : IFileSystemProvider
    {
        public Task<IReadOnlyList<FileSystemItem>> ListDirectoryAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<FileSystemItem>>([]);
        }
    }

    private enum PaneDirection
    {
        Left,
        Right,
    }
}
