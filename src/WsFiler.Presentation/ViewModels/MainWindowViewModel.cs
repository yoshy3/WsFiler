using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Commands;
using WsFiler.Core.Files;
using WsFiler.Presentation.Operations;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileSystemProvider fileSystemProvider;
    private readonly string defaultHome;

    [ObservableProperty]
    private string statusMessage = "Ready";

    public string StatusSummary => $"{LeftPane.Summary} | {RightPane.Summary}";

    public FilePaneViewModel LeftPane { get; } = new() { IsActive = true };

    public FilePaneViewModel RightPane { get; } = new();

    public MainWindowViewModel(IFileSystemProvider? fileSystemProvider = null)
    {
        this.fileSystemProvider = fileSystemProvider ?? new EmptyFileSystemProvider();
        defaultHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public Task InitializeAsync(string? leftPath = null, string? rightPath = null)
    {
        var left = string.IsNullOrWhiteSpace(leftPath) ? defaultHome : leftPath;
        var right = string.IsNullOrWhiteSpace(rightPath) ? defaultHome : rightPath;
        return LoadInitialPanesAsync(left, right);
    }

    public (string LeftPath, string RightPath) GetCurrentPanePaths()
    {
        return (LeftPane.CurrentPath, RightPane.CurrentPath);
    }

    public async Task HandleKeyAsync(string key)
    {
        var commandId = key switch
        {
            "Up" => ApplicationCommandId.CursorUp,
            "Down" => ApplicationCommandId.CursorDown,
            "Tab" => ApplicationCommandId.PaneSwitch,
            "Left" => ApplicationCommandId.CursorLeft,
            "Right" => ApplicationCommandId.CursorRight,
            "Enter" => ApplicationCommandId.DirectoryOpen,
            "Backspace" => ApplicationCommandId.DirectoryParent,
            "Space" => ApplicationCommandId.SelectionToggle,
            "A" => ApplicationCommandId.SelectionAll,
            "Escape" => ApplicationCommandId.SelectionClearAll,
            _ => null,
        };

        if (commandId is not null)
        {
            await HandleCommandAsync(commandId);
        }
    }

    public async Task HandleCommandAsync(string commandId)
    {
        switch (commandId)
        {
            case ApplicationCommandId.CursorUp:
                ActivePane.MoveCursor(-1);
                break;
            case ApplicationCommandId.CursorDown:
                ActivePane.MoveCursor(1);
                break;
            case ApplicationCommandId.PaneSwitch:
                SwitchActivePane();
                break;
            case ApplicationCommandId.CursorLeft:
                await HandleHorizontalAsync(PaneDirection.Left);
                break;
            case ApplicationCommandId.CursorRight:
                await HandleHorizontalAsync(PaneDirection.Right);
                break;
            case ApplicationCommandId.DirectoryOpen:
                await OpenCurrentDirectoryAsync();
                break;
            case ApplicationCommandId.DirectoryParent:
                await NavigateParentAsync();
                break;
            case ApplicationCommandId.SelectionToggle:
                ActivePane.ToggleCurrentSelectionAndMoveNext();
                OnPropertyChanged(nameof(StatusSummary));
                break;
            case ApplicationCommandId.SelectionAll:
                ActivePane.MarkAll();
                OnPropertyChanged(nameof(StatusSummary));
                break;
            case ApplicationCommandId.SelectionClearAll:
            case ApplicationCommandId.SelectionClear:
                ActivePane.ClearMarks();
                OnPropertyChanged(nameof(StatusSummary));
                break;
        }
    }

    public FilePaneViewModel ActivePane => LeftPane.IsActive ? LeftPane : RightPane;

    private FilePaneViewModel InactivePane => LeftPane.IsActive ? RightPane : LeftPane;

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

    public FileOperationRequest? CreateCopyRequest()
    {
        var targets = ActivePane.OperationTargets;
        if (targets.Count == 0)
        {
            StatusMessage = "No item to copy";
            return null;
        }

        return new FileOperationRequest(targets, InactivePane.CurrentPath);
    }

    public FileOperationRequest? CreateMoveRequest()
    {
        var targets = ActivePane.OperationTargets;
        if (targets.Count == 0)
        {
            StatusMessage = "No item to move";
            return null;
        }

        return new FileOperationRequest(targets, InactivePane.CurrentPath);
    }

    public DeleteRequest? CreateDeleteRequest()
    {
        var targets = ActivePane.OperationTargets;
        if (targets.Count == 0)
        {
            StatusMessage = "No item to delete";
            return null;
        }

        return new DeleteRequest(targets);
    }

    public RenameRequest? CreateRenameRequest()
    {
        var current = ActivePane.CurrentItem;
        if (current is null)
        {
            StatusMessage = "No item to rename";
            return null;
        }

        return new RenameRequest(current);
    }

    public async Task CopyAsync(
        FileOperationRequest request,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync)
    {
        try
        {
            var sourcePaths = request.Targets.Select(item => item.FullPath).ToList();
            await fileSystemProvider.CopyAsync(sourcePaths, request.DestinationDirectory, resolveConflictAsync);
            ActivePane.ClearMarks();
            await RefreshPaneAsync(InactivePane);
            StatusMessage = $"Copied {request.Targets.Count:N0} item(s)";
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async Task MoveAsync(
        FileOperationRequest request,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync)
    {
        try
        {
            var sourcePaths = request.Targets.Select(item => item.FullPath).ToList();
            await fileSystemProvider.MoveAsync(sourcePaths, request.DestinationDirectory, resolveConflictAsync);
            ActivePane.ClearMarks();
            await RefreshPaneAsync(InactivePane);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = $"Moved {request.Targets.Count:N0} item(s)";
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async Task DeleteAsync(DeleteRequest request)
    {
        try
        {
            var targetPaths = request.Targets.Select(item => item.FullPath).ToList();
            await fileSystemProvider.DeleteAsync(targetPaths);
            ActivePane.ClearMarks();
            await RefreshPaneAsync(ActivePane);
            StatusMessage = $"Deleted {request.Targets.Count:N0} item(s)";
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    public async Task RenameAsync(RenameRequest request, string newName)
    {
        try
        {
            await fileSystemProvider.RenameAsync(request.Target.FullPath, newName);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = $"Renamed {request.Target.Name} to {newName}";
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
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

    private async Task RefreshPaneAsync(FilePaneViewModel pane)
    {
        var items = await fileSystemProvider.ListDirectoryAsync(pane.CurrentPath);
        pane.Load(pane.CurrentPath, items);
    }

    private async Task LoadInitialPanesAsync(string leftPath, string rightPath)
    {
        await LoadPaneOrDefaultAsync(LeftPane, leftPath);
        await LoadPaneOrDefaultAsync(RightPane, rightPath);
        OnPropertyChanged(nameof(StatusSummary));
    }

    private async Task LoadPaneOrDefaultAsync(FilePaneViewModel pane, string path)
    {
        try
        {
            var items = await fileSystemProvider.ListDirectoryAsync(path);
            pane.Load(path, items);
        }
        catch
        {
            var items = await fileSystemProvider.ListDirectoryAsync(defaultHome);
            pane.Load(defaultHome, items);
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

        public Task CopyAsync(
            IReadOnlyList<string> sourcePaths,
            string destinationDirectory,
            Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MoveAsync(
            IReadOnlyList<string> sourcePaths,
            string destinationDirectory,
            Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            IReadOnlyList<string> targetPaths,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RenameAsync(
            string sourcePath,
            string newName,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private enum PaneDirection
    {
        Left,
        Right,
    }
}
