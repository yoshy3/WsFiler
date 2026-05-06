using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Commands;
using WsFiler.Core.Files;
using WsFiler.Presentation.Operations;
using WsFiler.Presentation.Resources;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileSystemProvider fileSystemProvider;
    private readonly string defaultHome;
    private int nextLogNumber = 1;

    [ObservableProperty]
    private string statusMessage = Strings.Status_Ready;

    [ObservableProperty]
    private bool isLogVisible;

    public string StatusSummary => $"{LeftPane.Summary} | {RightPane.Summary}";

    public FilePaneViewModel LeftPane { get; } = new() { IsActive = true };

    public FilePaneViewModel RightPane { get; } = new();

    public ObservableCollection<LogEntryViewModel> Logs { get; } = [];

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
            "A" => ApplicationCommandId.FileAttributes,
            "V" => ApplicationCommandId.LogToggle,
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
            case ApplicationCommandId.LogToggle:
                IsLogVisible = !IsLogVisible;
                break;
            case ApplicationCommandId.DirectoryRoot:
                await NavigateRootAsync();
                break;
            case ApplicationCommandId.PaneSyncOpposite:
                await LoadPaneAsync(ActivePane, InactivePane.CurrentPath);
                break;
            case ApplicationCommandId.ViewSort:
                break;
        }
    }

    public FilePaneViewModel ActivePane => LeftPane.IsActive ? LeftPane : RightPane;

    private FilePaneViewModel InactivePane => LeftPane.IsActive ? RightPane : LeftPane;

    public void ActivateLeftPane(FileItemViewModel? selectedItem = null)
    {
        ActivatePane(LeftPane, RightPane, selectedItem);
    }

    public void ActivateRightPane(FileItemViewModel? selectedItem = null)
    {
        ActivatePane(RightPane, LeftPane, selectedItem);
    }

    private void SwitchActivePane()
    {
        LeftPane.IsActive = !LeftPane.IsActive;
        RightPane.IsActive = !RightPane.IsActive;
    }

    private static void ActivatePane(
        FilePaneViewModel activePane,
        FilePaneViewModel inactivePane,
        FileItemViewModel? selectedItem)
    {
        inactivePane.IsActive = false;
        activePane.IsActive = true;

        if (selectedItem is not null)
        {
            activePane.MoveCursorTo(selectedItem);
        }
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

        if (direction == PaneDirection.Left)
        {
            ActivateLeftPane();
        }
        else
        {
            ActivateRightPane();
        }
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
            StatusMessage = Strings.Status_PreviewUnavailable;
            return;
        }

        await LoadPaneAsync(ActivePane, current.FullPath);
    }

    public FileOperationRequest? CreateCopyRequest()
    {
        var targets = ActivePane.OperationTargets;
        if (targets.Count == 0)
        {
            StatusMessage = Strings.Status_NoItemToCopy;
            return null;
        }

        return new FileOperationRequest(targets, InactivePane.CurrentPath);
    }

    public FileOperationRequest? CreateMoveRequest()
    {
        var targets = ActivePane.OperationTargets;
        if (targets.Count == 0)
        {
            StatusMessage = Strings.Status_NoItemToMove;
            return null;
        }

        return new FileOperationRequest(targets, InactivePane.CurrentPath);
    }

    public DeleteRequest? CreateDeleteRequest()
    {
        var targets = ActivePane.OperationTargets;
        if (targets.Count == 0)
        {
            StatusMessage = Strings.Status_NoItemToDelete;
            return null;
        }

        return new DeleteRequest(targets);
    }

    public RenameRequest? CreateRenameRequest()
    {
        var current = ActivePane.CurrentItem;
        if (current is null)
        {
            StatusMessage = Strings.Status_NoItemToRename;
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
            StatusMessage = string.Format(Strings.Status_Copied, request.Targets.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
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
            StatusMessage = string.Format(Strings.Status_Moved, request.Targets.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
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
            StatusMessage = string.Format(Strings.Status_Deleted, request.Targets.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task RenameAsync(RenameRequest request, string newName)
    {
        try
        {
            await fileSystemProvider.RenameAsync(request.Target.FullPath, newName);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = string.Format(Strings.Status_Renamed, request.Target.Name, newName);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    private async Task NavigateRootAsync()
    {
        var root = Path.GetPathRoot(ActivePane.CurrentPath);
        if (string.IsNullOrEmpty(root))
        {
            StatusMessage = Strings.Status_AlreadyAtRoot;
            return;
        }

        await LoadPaneAsync(ActivePane, root);
    }

    public async Task CreateDirectoryAsync(string name)
    {
        try
        {
            var path = Path.Combine(ActivePane.CurrentPath, name);
            await fileSystemProvider.CreateDirectoryAsync(path);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = string.Format(Strings.Status_DirectoryCreated, name);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task CreateFileAsync(string name)
    {
        try
        {
            var path = Path.Combine(ActivePane.CurrentPath, name);
            await fileSystemProvider.CreateFileAsync(path);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = string.Format(Strings.Status_FileCreated, name);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task ApplyFilterAsync(string? pattern)
    {
        try
        {
            ActivePane.ApplyFilter(pattern);
            var items = await fileSystemProvider.ListDirectoryAsync(ActivePane.CurrentPath);
            ActivePane.Load(ActivePane.CurrentPath, items);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task DuplicateAsync(
        Func<Core.Files.FileConflictInfo, Task<Core.Files.FileConflictDecision>> resolveConflictAsync)
    {
        var current = ActivePane.CurrentItem;
        if (current is null)
        {
            StatusMessage = Strings.Status_NoItemToRename;
            return;
        }

        try
        {
            await fileSystemProvider.CopyAsync(
                [current.FullPath],
                ActivePane.CurrentPath,
                resolveConflictAsync);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = string.Format(Strings.Status_Duplicated, current.Name);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task SetAttributesAsync(string path, FileAttributes attributes)
    {
        try
        {
            await fileSystemProvider.SetAttributesAsync(path, attributes);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = Strings.Status_AttributesChanged;
            LogInfo(StatusMessage);
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public Task<FileAttributes> GetAttributesAsync(string path)
    {
        return fileSystemProvider.GetAttributesAsync(path);
    }

    private async Task NavigateParentAsync()
    {
        var parent = Directory.GetParent(ActivePane.CurrentPath);
        if (parent is null)
        {
            StatusMessage = Strings.Status_AlreadyAtRoot;
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
            LogError(ex.Message);
        }
    }

    public Task RefreshActivePaneAsync() => RefreshPaneAsync(ActivePane);

    public Task NavigateActivePaneAsync(string path) => LoadPaneAsync(ActivePane, path);

    public async Task ApplySortAsync(PaneSortField field, bool ascending)
    {
        ActivePane.SetSort(field, ascending);
        await RefreshPaneAsync(ActivePane);
        StatusMessage = string.Format(Strings.Status_SortChanged, ActivePane.SortField);
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

    public void LogInfo(string message)
    {
        Logs.Add(new LogEntryViewModel(nextLogNumber++, "INFO", message));
        TrimLogs();
    }

    public void LogError(string message)
    {
        StatusMessage = message;
        Logs.Add(new LogEntryViewModel(nextLogNumber++, "ERROR", message));
        IsLogVisible = true;
        TrimLogs();
    }

    private void TrimLogs()
    {
        while (Logs.Count > 200)
        {
            Logs.RemoveAt(0);
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

        public Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task CreateFileAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<FileAttributes> GetAttributesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(FileAttributes.Normal);

        public Task SetAttributesAsync(string path, FileAttributes attributes, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private enum PaneDirection
    {
        Left,
        Right,
    }
}
