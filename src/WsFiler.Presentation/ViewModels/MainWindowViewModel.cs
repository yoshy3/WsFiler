using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WsFiler.Core.Commands;
using WsFiler.Core.Files;
using WsFiler.Presentation.Operations;
using WsFiler.Presentation.Resources;

namespace WsFiler.Presentation.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private const int MaxDirectoryHistoryCount = 50;

    private readonly IFileSystemProvider fileSystemProvider;
    private readonly string defaultHome;
    private readonly List<string> directoryHistory = [];
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
            case ApplicationCommandId.CursorPageUp:
                ActivePane.MoveCursorPage(10, -1);
                break;
            case ApplicationCommandId.CursorPageDown:
                ActivePane.MoveCursorPage(10, 1);
                break;
            case ApplicationCommandId.CursorFirst:
                ActivePane.MoveCursorFirst();
                break;
            case ApplicationCommandId.CursorLast:
                ActivePane.MoveCursorLast();
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
            case ApplicationCommandId.ViewRefresh:
                await RefreshActivePaneAsync();
                StatusMessage = ActivePane.CurrentPath;
                OnPropertyChanged(nameof(StatusSummary));
                break;
        }
    }

    public FilePaneViewModel ActivePane => LeftPane.IsActive ? LeftPane : RightPane;

    private FilePaneViewModel InactivePane => LeftPane.IsActive ? RightPane : LeftPane;

    public void MoveActivePanePage(int pageSize, int direction)
    {
        ActivePane.MoveCursorPage(pageSize, direction);
    }

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

        if (current.IsParent)
        {
            await NavigateParentAsync();
            return;
        }

        if (!current.IsDirectory && !await fileSystemProvider.CanListDirectoryAsync(current.FullPath))
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

    public async Task<bool> CanListCurrentItemAsync()
    {
        var current = ActivePane.CurrentItem;
        return current is not null &&
            (current.IsDirectory || await fileSystemProvider.CanListDirectoryAsync(current.FullPath));
    }

    public async Task CopyAsync(
        FileOperationRequest request,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sourcePaths = request.Targets.Select(item => item.FullPath).ToList();
            await fileSystemProvider.CopyAsync(
                sourcePaths,
                request.DestinationDirectory,
                resolveConflictAsync,
                progress,
                cancellationToken);
            ActivePane.ClearMarks();
            await RefreshPaneAsync(InactivePane);
            StatusMessage = string.Format(Strings.Status_Copied, request.Targets.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.Status_OperationCanceled;
            LogInfo(StatusMessage);
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task CopyExternalFilesAsync(
        IReadOnlyList<string> sourcePaths,
        FilePaneViewModel destinationPane,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (sourcePaths.Count == 0)
        {
            return;
        }

        try
        {
            await fileSystemProvider.CopyAsync(
                sourcePaths,
                destinationPane.CurrentPath,
                resolveConflictAsync,
                progress,
                cancellationToken);
            await RefreshPaneAsync(destinationPane);
            StatusMessage = string.Format(Strings.Status_Copied, sourcePaths.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.Status_OperationCanceled;
            LogInfo(StatusMessage);
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task MoveAsync(
        FileOperationRequest request,
        Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
        IProgress<FileOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var sourcePaths = request.Targets.Select(item => item.FullPath).ToList();
            await fileSystemProvider.MoveAsync(
                sourcePaths,
                request.DestinationDirectory,
                resolveConflictAsync,
                progress,
                cancellationToken);
            ActivePane.ClearMarks();
            await RefreshPaneAsync(InactivePane);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = string.Format(Strings.Status_Moved, request.Targets.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.Status_OperationCanceled;
            LogInfo(StatusMessage);
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public async Task DeleteAsync(
        DeleteRequest request,
        Func<FileDeleteConfirmationInfo, Task<FileDeleteConfirmationDecision>>? confirmDeleteAsync = null)
    {
        try
        {
            var targetPaths = request.Targets.Select(item => item.FullPath).ToList();
            await fileSystemProvider.DeleteAsync(targetPaths, confirmDeleteAsync);
            ActivePane.ClearMarks();
            await RefreshPaneAsync(ActivePane);
            StatusMessage = string.Format(Strings.Status_Deleted, request.Targets.Count);
            LogInfo(StatusMessage);
            OnPropertyChanged(nameof(StatusSummary));
        }
        catch (OperationCanceledException)
        {
            StatusMessage = Strings.Status_OperationCanceled;
            LogInfo(StatusMessage);
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

    public Task ApplyFilterAsync(string? pattern) => ApplyFilterAsync(pattern, ActivePane.ShowHiddenFiles);

    public async Task ApplyFilterAsync(string? pattern, bool showHiddenFiles)
    {
        try
        {
            ActivePane.SetShowHiddenFiles(showHiddenFiles);
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

    public async Task SetUnixFileModeAsync(string path, UnixFileMode mode)
    {
        try
        {
            await fileSystemProvider.SetUnixFileModeAsync(path, mode);
            await RefreshPaneAsync(ActivePane);
            StatusMessage = Strings.Status_AttributesChanged;
            LogInfo(StatusMessage);
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
        }
    }

    public Task<UnixFileMode> GetUnixFileModeAsync(string path)
    {
        return fileSystemProvider.GetUnixFileModeAsync(path);
    }

    public Task<bool> CanSetUnixFileModeAsync(string path)
    {
        return fileSystemProvider.CanSetUnixFileModeAsync(path);
    }

    public Task<Stream> OpenReadAsync(string path)
    {
        return fileSystemProvider.OpenReadAsync(path);
    }

    private async Task NavigateParentAsync()
    {
        if (ActivePane.IsVirtualDirectory)
        {
            var returnPath = ActivePane.VirtualReturnPath;
            if (string.IsNullOrWhiteSpace(returnPath))
            {
                StatusMessage = Strings.Status_AlreadyAtRoot;
                return;
            }

            await LoadPaneAsync(ActivePane, returnPath);
            return;
        }

        var parent = fileSystemProvider.GetParentPath(ActivePane.CurrentPath);
        if (string.IsNullOrEmpty(parent))
        {
            StatusMessage = Strings.Status_AlreadyAtRoot;
            return;
        }

        var leaf = fileSystemProvider.GetFileName(ActivePane.CurrentPath);
        if (!string.IsNullOrEmpty(leaf))
        {
            ActivePane.RememberCursorForPath(parent, leaf);
        }

        await LoadPaneAsync(ActivePane, parent);
    }

    private async Task LoadPaneAsync(FilePaneViewModel pane, string path)
    {
        try
        {
            var items = await fileSystemProvider.ListDirectoryAsync(path);
            pane.ClearFilter();
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

    public void LoadSearchResultsAsVirtualDirectory(
        string baseDirectory,
        IReadOnlyList<string> resultPaths)
    {
        var items = resultPaths
            .Select(path => TryCreateFileSystemItem(path, baseDirectory))
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
        var displayPath = string.Format(Strings.Pane_SearchResultsPath, baseDirectory);
        ActivePane.LoadVirtual(displayPath, baseDirectory, items);
        StatusMessage = string.Format(Strings.Status_SearchResultsLoaded, items.Count);
        OnPropertyChanged(nameof(StatusSummary));
    }

    public async Task NavigateActivePaneToItemAsync(string fullPath)
    {
        var parent = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return;
        }

        await LoadPaneAsync(ActivePane, parent);
        ActivePane.MoveCursorToPath(fullPath);
    }

    public async Task ApplySortAsync(PaneSortField field, bool ascending)
    {
        ActivePane.SetSort(field, ascending);
        await RefreshPaneAsync(ActivePane);
        StatusMessage = string.Format(Strings.Status_SortChanged, ActivePane.SortField);
    }

    private async Task RefreshPaneAsync(FilePaneViewModel pane)
    {
        if (pane.IsVirtualDirectory)
        {
            var returnPath = pane.VirtualReturnPath;
            if (string.IsNullOrWhiteSpace(returnPath))
            {
                return;
            }

            var displayPath = pane.CurrentPath;
            var paths = pane.Items
                .Where(item => !item.IsParent)
                .Select(item => item.FullPath)
                .ToList();
            var virtualItems = paths
                .Select(TryCreateFileSystemItem)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();
            pane.LoadVirtual(displayPath, returnPath, virtualItems);
            return;
        }

        var directoryItems = await fileSystemProvider.ListDirectoryAsync(pane.CurrentPath);
        pane.Load(pane.CurrentPath, directoryItems);
    }

    private static FileSystemItem? TryCreateFileSystemItem(string path)
    {
        return TryCreateFileSystemItem(path, baseDirectory: null);
    }

    private static FileSystemItem? TryCreateFileSystemItem(string path, string? baseDirectory)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var directory = new DirectoryInfo(path);
                return CreateFileSystemItem(directory, GetDisplayName(path, baseDirectory));
            }

            if (File.Exists(path))
            {
                var file = new FileInfo(path);
                return CreateFileSystemItem(file, GetDisplayName(path, baseDirectory));
            }
        }
        catch
        {
        }

        return null;
    }

    private static FileSystemItem CreateFileSystemItem(FileSystemInfo info, string? displayName = null)
    {
        var attributes = info.Attributes;
        var isDirectory = attributes.HasFlag(FileAttributes.Directory);
        var isSymbolicLink = attributes.HasFlag(FileAttributes.ReparsePoint);
        var type = isSymbolicLink
            ? FileSystemItemType.SymbolicLink
            : isDirectory
                ? FileSystemItemType.Directory
                : FileSystemItemType.File;
        var size = info is FileInfo fileInfo ? fileInfo.Length : (long?)null;
        var name = string.IsNullOrWhiteSpace(displayName) ? info.Name : displayName;
        return new FileSystemItem(
            name,
            info.FullName,
            type,
            size,
            info.LastWriteTime,
            isDirectory ? "" : GetExtensionWithoutDot(name),
            attributes.HasFlag(FileAttributes.Hidden),
            attributes.HasFlag(FileAttributes.ReadOnly),
            attributes.HasFlag(FileAttributes.System));
    }

    private static string? GetDisplayName(string path, string? baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var relative = Path.GetRelativePath(baseDirectory, path);
        return string.IsNullOrWhiteSpace(relative) || relative == "."
            ? null
            : relative;
    }

    private static string GetExtensionWithoutDot(string name)
    {
        var lastDot = name.LastIndexOf('.');
        return lastDot <= 0 ? string.Empty : name[(lastDot + 1)..];
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

    public IReadOnlyList<string> DirectoryHistory => directoryHistory;

    public void SetDirectoryHistory(IEnumerable<string>? paths)
    {
        directoryHistory.Clear();
        if (paths is null)
        {
            return;
        }

        foreach (var path in paths.Reverse())
        {
            RememberDirectory(path);
        }
    }

    public bool RecordActiveDirectoryInHistory()
    {
        return RememberDirectory(ActivePane.CurrentPath);
    }

    public bool SearchActivePaneByName(string query)
    {
        var found = ActivePane.MoveCursorToFirstNameMatch(query);
        StatusMessage = found ? query : string.Format(Strings.Status_SearchNotFound, query);
        return found;
    }

    private bool RememberDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var oldIndex = directoryHistory.FindIndex(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        if (oldIndex == 0)
        {
            return false;
        }

        directoryHistory.RemoveAll(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase));
        directoryHistory.Insert(0, path);
        if (directoryHistory.Count > MaxDirectoryHistoryCount)
        {
            directoryHistory.RemoveRange(
                MaxDirectoryHistoryCount,
                directoryHistory.Count - MaxDirectoryHistoryCount);
        }

        return true;
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

        public Task<bool> CanListDirectoryAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public string? GetParentPath(string path)
        {
            return Directory.GetParent(path)?.FullName;
        }

        public string GetFileName(string path)
        {
            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(Stream.Null);
        }

        public Task CopyAsync(
            IReadOnlyList<string> sourcePaths,
            string destinationDirectory,
            Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
            IProgress<FileOperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task MoveAsync(
            IReadOnlyList<string> sourcePaths,
            string destinationDirectory,
            Func<FileConflictInfo, Task<FileConflictDecision>> resolveConflictAsync,
            IProgress<FileOperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            IReadOnlyList<string> targetPaths,
            Func<FileDeleteConfirmationInfo, Task<FileDeleteConfirmationDecision>>? confirmDeleteAsync = null,
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

        public Task<UnixFileMode> GetUnixFileModeAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);

        public Task SetUnixFileModeAsync(string path, UnixFileMode mode, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> CanSetUnixFileModeAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private enum PaneDirection
    {
        Left,
        Right,
    }
}
