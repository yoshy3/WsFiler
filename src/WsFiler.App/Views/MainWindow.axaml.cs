using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using System;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WsFiler.Core.Commands;
using WsFiler.Core.Files;
using WsFiler.Core.KeyMap;
using WsFiler.Infra.Settings;
using WsFiler.Presentation.Resources;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App.Views;

public partial class MainWindow : Window
{
    private const int PreviewByteLimit = 100 * 1024;

    private readonly Dictionary<string, string> keyToCommandMap;
    private readonly Dictionary<FileItemViewModel, List<DataGridRow>> itemRows = [];
    private bool isClearingGridSelection;

    public MainWindow(IReadOnlyDictionary<string, string>? customKeyMap = null)
    {
        InitializeComponent();
        SetupWindowIcon();
        ApplyTitle();
        ApplyLocalizedText();
        keyToCommandMap = BuildKeyMap(customKeyMap);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += (_, _) => OnDataContextChanged();
        Loaded += (_, _) => UpdatePaneVisualState();
        Focusable = true;
    }

    public MainWindow()
    {
        InitializeComponent();
        SetupWindowIcon();
        ApplyTitle();
        ApplyLocalizedText();
        keyToCommandMap = BuildKeyMap(null);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += (_, _) => OnDataContextChanged();
        Loaded += (_, _) => UpdatePaneVisualState();
        Focusable = true;
    }

    private void SetupWindowIcon()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        using var stream = AssetLoader.Open(new Uri("avares://WsFiler.App/Assets/linux/wsfiler-256.png"));
        Icon = new WindowIcon(stream);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var key = NormalizeKeyWithModifiers(e.Key, e.KeyModifiers);
        if (!TryResolveCommand(key, out var commandId))
        {
            return;
        }

        e.Handled = true;
        if (ShouldRecordWorkingDirectory(commandId))
        {
            viewModel.RecordActiveDirectoryInHistory();
        }

        if (commandId == ApplicationCommandId.FileCopy)
        {
            await ConfirmAndCopyAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileMove)
        {
            await ConfirmAndMoveAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileDelete)
        {
            await ConfirmAndDeleteAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileRename)
        {
            await ConfirmAndRenameAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.DirectoryOpen &&
                 viewModel.ActivePane.CurrentItem is { IsDirectory: false } currentItem)
        {
            await PreviewTextFileAsync(currentItem);
        }
        else if (commandId == ApplicationCommandId.DriveChange)
        {
            await ShowDriveSelectDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.DirectoryCreate)
        {
            await ShowCreateDirectoryDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileCreate)
        {
            await ShowCreateFileDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileAttributes)
        {
            await ShowAttributeDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileFilter)
        {
            await ShowFilterDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.EditorLaunch)
        {
            await LaunchEditorAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileDuplicate)
        {
            await DuplicateCurrentItemAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.ViewSort)
        {
            await ShowSortDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.AppExit)
        {
            Close();
            return;
        }
        else if (commandId == ApplicationCommandId.FileExecute)
        {
            await ExecuteCurrentItemAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileCopyPath)
        {
            await CopyCurrentPathAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.TerminalOpen)
        {
            LaunchTerminal(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileSearch)
        {
            await ShowSearchDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.DirectoryBookmark)
        {
            await ShowBookmarkDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.DirectoryHistory)
        {
            await ShowDirectoryHistoryDialogAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileProperties)
        {
            await ShowPropertiesAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.FileCompare)
        {
            await CompareCurrentItemsAsync(viewModel);
        }
        else if (commandId == ApplicationCommandId.CursorPageUp)
        {
            viewModel.MoveActivePanePage(GetVisibleFileRowCount(viewModel), -1);
        }
        else if (commandId == ApplicationCommandId.CursorPageDown)
        {
            viewModel.MoveActivePanePage(GetVisibleFileRowCount(viewModel), 1);
        }
        else
        {
            await viewModel.HandleCommandAsync(commandId);
        }

        ScrollActiveSelectionIntoView(viewModel);
        RefreshCursorUnderlines();
        UpdatePaneVisualState(viewModel);
    }

    private void ApplyLocalizedText()
    {
        ApplyLocalizedColumnHeaders(LeftFileGrid);
        ApplyLocalizedColumnHeaders(RightFileGrid);
    }

    private void ApplyTitle()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var version = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString()
            : informationalVersion;

        Title = string.IsNullOrWhiteSpace(version) ? "WsFilter" : $"WsFilter v{NormalizeVersionText(version)}";
    }

    private static string NormalizeVersionText(string version)
    {
        var normalized = version.Split('+', 2)[0].Trim();
        return normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? normalized[1..]
            : normalized;
    }

    private void OnDataContextChanged()
    {
        UpdatePaneVisualState();

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Logs.CollectionChanged += OnLogsCollectionChanged;
        }
    }

    private void OnLogsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.Logs.Count > 0)
        {
            LogListBox.ScrollIntoView(viewModel.Logs[^1]);
        }
    }

    private static void ApplyLocalizedColumnHeaders(DataGrid grid)
    {
        grid.Columns[1].Header = Strings.Grid_Column_Name;
        grid.Columns[2].Header = Strings.Grid_Column_Ext;
        grid.Columns[3].Header = Strings.Grid_Column_Size;
        grid.Columns[4].Header = Strings.Grid_Column_Modified;
    }

    private void OnLeftFileGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isClearingGridSelection ||
            DataContext is not MainWindowViewModel viewModel ||
            LeftFileGrid.SelectedItem is not FileItemViewModel selectedItem)
        {
            return;
        }

        viewModel.ActivateLeftPane(selectedItem);
        ClearGridSelection(LeftFileGrid);
        RefreshCursorUnderlines();
        UpdatePaneVisualState(viewModel);
        Focus();
    }

    private void OnRightFileGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (isClearingGridSelection ||
            DataContext is not MainWindowViewModel viewModel ||
            RightFileGrid.SelectedItem is not FileItemViewModel selectedItem)
        {
            return;
        }

        viewModel.ActivateRightPane(selectedItem);
        ClearGridSelection(RightFileGrid);
        RefreshCursorUnderlines();
        UpdatePaneVisualState(viewModel);
        Focus();
    }

    private void OnFileGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is not FileItemViewModel item)
        {
            return;
        }

        ApplyCursorUnderline(e.Row, item);
        if (!itemRows.TryGetValue(item, out var rows))
        {
            rows = [];
            itemRows[item] = rows;
        }

        rows.Add(e.Row);
        item.PropertyChanged += OnFileGridRowItemPropertyChanged;
    }

    private void OnFileGridUnloadingRow(object? sender, DataGridRowEventArgs e)
    {
        if (e.Row.DataContext is FileItemViewModel item)
        {
            item.PropertyChanged -= OnFileGridRowItemPropertyChanged;
            if (itemRows.TryGetValue(item, out var rows))
            {
                rows.Remove(e.Row);
                if (rows.Count == 0)
                {
                    itemRows.Remove(item);
                }
            }
        }
    }

    private void OnFileGridRowItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FileItemViewModel.IsCursor) ||
            sender is not FileItemViewModel item)
        {
            return;
        }

        if (!itemRows.TryGetValue(item, out var rows))
        {
            return;
        }

        foreach (var row in rows)
        {
            ApplyCursorUnderline(row, item);
        }
    }

    private static void ApplyCursorUnderline(DataGridRow row, FileItemViewModel item)
    {
        row.Height = 20;
        row.MinHeight = 20;
        row.MaxHeight = 20;
        row.BorderBrush = item.IsCursor
            ? new SolidColorBrush(Colors.White)
            : Brushes.Transparent;
        row.BorderThickness = item.IsCursor
            ? new Thickness(0, 0, 0, 1)
            : new Thickness(0);
    }

    private void ClearGridSelection(DataGrid grid)
    {
        isClearingGridSelection = true;
        grid.SelectedItem = null;
        isClearingGridSelection = false;
    }

    private void RefreshCursorUnderlines()
    {
        foreach (var pair in itemRows)
        {
            foreach (var row in pair.Value)
            {
                ApplyCursorUnderline(row, pair.Key);
            }
        }
    }

    private async Task ConfirmAndCopyAsync(MainWindowViewModel viewModel)
    {
        var request = viewModel.CreateCopyRequest();
        if (request is null)
        {
            return;
        }

        var dialog = new CopyConfirmDialog(
            request.RepresentativeName,
            request.Targets.Count,
            request.DestinationDirectory);
        var confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed)
        {
            await viewModel.CopyAsync(request, ResolveConflictAsync);
        }
    }

    private async Task ConfirmAndMoveAsync(MainWindowViewModel viewModel)
    {
        var request = viewModel.CreateMoveRequest();
        if (request is null)
        {
            return;
        }

        var dialog = new MoveConfirmDialog(
            request.RepresentativeName,
            request.Targets.Count,
            request.DestinationDirectory);
        var confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed)
        {
            await viewModel.MoveAsync(request, ResolveConflictAsync);
        }
    }

    private async Task<FileConflictDecision> ResolveConflictAsync(FileConflictInfo conflict)
    {
        var dialog = new ConflictConfirmDialog(conflict);
        var decision = await dialog.ShowDialog<FileConflictDecision?>(this);
        return decision ?? new FileConflictDecision(FileConflictAction.Cancel, ApplyToAll: false);
    }

    private async Task ConfirmAndDeleteAsync(MainWindowViewModel viewModel)
    {
        var request = viewModel.CreateDeleteRequest();
        if (request is null)
        {
            return;
        }

        var dialog = new DeleteConfirmDialog(
            request.RepresentativeName,
            request.Targets.Count);
        var confirmed = await dialog.ShowDialog<bool>(this);

        if (confirmed)
        {
            await viewModel.DeleteAsync(request);
        }
    }

    private async Task ConfirmAndRenameAsync(MainWindowViewModel viewModel)
    {
        var request = viewModel.CreateRenameRequest();
        if (request is null)
        {
            return;
        }

        var dialog = new RenameDialog(request.Target.Name);
        var newName = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrWhiteSpace(newName) && newName != request.Target.Name)
        {
            await viewModel.RenameAsync(request, newName);
        }
    }

    private async Task PreviewTextFileAsync(FileItemViewModel item)
    {
        try
        {
            var (text, isTruncated) = await ReadPreviewTextAsync(item.FullPath);
            var dialog = new TextPreviewDialog(item.FullPath, text, isTruncated);
            dialog.FitToOwner(this);
            await dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.LogError(ex.Message);
            }
        }
    }

    private async Task ShowDriveSelectDialogAsync(MainWindowViewModel viewModel)
    {
        var dialog = new DriveSelectDialog(viewModel.ActivePane.CurrentPath);
        var root = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrEmpty(root))
        {
            await viewModel.NavigateActivePaneAsync(root);
        }
    }

    private async Task ShowCreateDirectoryDialogAsync(MainWindowViewModel viewModel)
    {
        var dialog = new InputDialog(
            Strings.Dialog_DirectoryCreate_Title,
            Strings.Dialog_DirectoryCreate_Prompt);
        var name = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(name))
        {
            await viewModel.CreateDirectoryAsync(name);
        }
    }

    private async Task ShowCreateFileDialogAsync(MainWindowViewModel viewModel)
    {
        var dialog = new InputDialog(
            Strings.Dialog_FileCreate_Title,
            Strings.Dialog_FileCreate_Prompt);
        var name = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(name))
        {
            await viewModel.CreateFileAsync(name);
        }
    }

    private async Task ShowAttributeDialogAsync(MainWindowViewModel viewModel)
    {
        var current = viewModel.ActivePane.CurrentItem;
        if (current is null)
        {
            return;
        }

        try
        {
            var attrs = await viewModel.GetAttributesAsync(current.FullPath);
            var dialog = new AttributeDialog(current.Name, attrs);
            var newAttrs = await dialog.ShowDialog<FileAttributes?>(this);
            if (newAttrs.HasValue)
            {
                await viewModel.SetAttributesAsync(current.FullPath, newAttrs.Value);
            }
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }
    }

    private async Task ShowFilterDialogAsync(MainWindowViewModel viewModel)
    {
        var dialog = new FilterDialog(viewModel.ActivePane.ShowHiddenFiles);
        var result = await dialog.ShowDialog<FilterDialogResult?>(this);
        if (result is not null)
        {
            var pattern = string.IsNullOrWhiteSpace(result.Pattern) ? null : result.Pattern;
            await viewModel.ApplyFilterAsync(pattern, result.ShowHiddenFiles);
        }
    }

    private async void OnFileGridDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (sender is DataGrid grid && grid.SelectedItem is FileItemViewModel item)
        {
            viewModel.ActivePane.MoveCursorTo(item);
        }

        e.Handled = true;
        viewModel.RecordActiveDirectoryInHistory();

        var current = viewModel.ActivePane.CurrentItem;
        if (current is { IsDirectory: false })
        {
            await PreviewTextFileAsync(current);
        }
        else
        {
            await viewModel.HandleCommandAsync(ApplicationCommandId.DirectoryOpen);
        }

        ScrollActiveSelectionIntoView(viewModel);
        RefreshCursorUnderlines();
        UpdatePaneVisualState(viewModel);
    }

    private async Task ShowSortDialogAsync(MainWindowViewModel viewModel)
    {
        var pane = viewModel.ActivePane;
        var dialog = new SortDialog(pane.SortField, pane.SortAscending);
        var result = await dialog.ShowDialog<(PaneSortField Field, bool Ascending)?>(this);
        if (result.HasValue)
        {
            await viewModel.ApplySortAsync(result.Value.Field, result.Value.Ascending);
        }
    }

    private Task ExecuteCurrentItemAsync(MainWindowViewModel viewModel)
    {
        var current = viewModel.ActivePane.CurrentItem;
        if (current is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo(current.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }

        return Task.CompletedTask;
    }

    private async Task CopyCurrentPathAsync(MainWindowViewModel viewModel)
    {
        var text = viewModel.ActivePane.CurrentItem?.FullPath ?? viewModel.ActivePane.CurrentPath;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(text);
        viewModel.StatusMessage = string.Format(Strings.Status_PathCopied, text);
    }

    private void LaunchTerminal(MainWindowViewModel viewModel)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                LaunchWindowsTerminal(viewModel.ActivePane.CurrentPath);
                return;
            }

            var fileName = "x-terminal-emulator";
            Process.Start(new ProcessStartInfo(fileName)
            {
                WorkingDirectory = viewModel.ActivePane.CurrentPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }
    }

    private static void LaunchWindowsTerminal(string workingDirectory)
    {
        try
        {
            Process.Start(new ProcessStartInfo("wt.exe")
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
            });
            return;
        }
        catch
        {
        }

        Process.Start(new ProcessStartInfo("cmd.exe", $"/k cd /d \"{workingDirectory}\"")
        {
            UseShellExecute = true,
        });
    }

    private async Task ShowSearchDialogAsync(MainWindowViewModel viewModel)
    {
        var dialog = new FileSearchDialog(viewModel.ActivePane.CurrentPath);
        var path = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.NavigateActivePaneToItemAsync(path);
        }
    }

    private async Task ShowBookmarkDialogAsync(MainWindowViewModel viewModel)
    {
        var settings = SettingsManager.Load();
        settings.DirectoryBookmarks ??= [];

        var dialog = new DirectoryBookmarkDialog(
            viewModel.ActivePane.CurrentPath,
            settings.DirectoryBookmarks);
        var result = await dialog.ShowDialog<DirectoryBookmarkDialogResult?>(this);
        if (result is null)
        {
            return;
        }

        settings.DirectoryBookmarks = result.Bookmarks.ToList();
        SettingsManager.Save(settings);

        if (!string.IsNullOrWhiteSpace(result.JumpPath))
        {
            await viewModel.NavigateActivePaneAsync(result.JumpPath);
        }
    }

    private async Task ShowDirectoryHistoryDialogAsync(MainWindowViewModel viewModel)
    {
        var dialog = new DirectoryHistoryDialog(viewModel.DirectoryHistory);
        var path = await dialog.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(path))
        {
            await viewModel.NavigateActivePaneAsync(path);
        }
    }

    private async Task ShowPropertiesAsync(MainWindowViewModel viewModel)
    {
        var current = viewModel.ActivePane.CurrentItem;
        if (current is null)
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            ShowWindowsProperties(current.FullPath, viewModel);
            return;
        }

        var info = BuildPropertiesText(current);
        var dialog = new TextPreviewDialog(current.FullPath, info, isTruncated: false);
        dialog.FitToOwner(this);
        await dialog.ShowDialog(this);
    }

    private static void ShowWindowsProperties(string path, MainWindowViewModel viewModel)
    {
        try
        {
            if (!NativeMethods.ShowProperties(path))
            {
                throw new InvalidOperationException(Strings.Status_PropertiesUnavailable);
            }
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }
    }

    private static string BuildPropertiesText(FileItemViewModel item)
    {
        var attributes = File.GetAttributes(item.FullPath);
        var size = item.IsDirectory ? "" : $"{new FileInfo(item.FullPath).Length:N0} bytes";
        return string.Join(Environment.NewLine, [
            $"Name: {item.Name}",
            $"Path: {item.FullPath}",
            $"Type: {(item.IsDirectory ? "Directory" : "File")}",
            $"Size: {size}",
            $"Modified: {item.Modified}",
            $"Attributes: {attributes}",
        ]);
    }

    private async Task CompareCurrentItemsAsync(MainWindowViewModel viewModel)
    {
        var targets = ResolveCompareTargets(viewModel);
        if (targets is null)
        {
            viewModel.StatusMessage = Strings.Status_CompareRequiresFiles;
            return;
        }

        try
        {
            var dialog = await DiffViewerDialog.CreateAsync(targets.Value.Left.FullPath, targets.Value.Right.FullPath);
            dialog.FitToOwner(this);
            await dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }
    }

    private static (FileItemViewModel Left, FileItemViewModel Right)? ResolveCompareTargets(MainWindowViewModel viewModel)
    {
        var leftMarked = viewModel.LeftPane.Items.Where(item => item.IsMarked && !item.IsDirectory).ToList();
        var rightMarked = viewModel.RightPane.Items.Where(item => item.IsMarked && !item.IsDirectory).ToList();

        if (leftMarked.Count > 0 && rightMarked.Count > 0)
        {
            return (leftMarked[0], rightMarked[0]);
        }

        var activeMarked = viewModel.ActivePane.Items.Where(item => item.IsMarked && !item.IsDirectory).ToList();
        if (activeMarked.Count >= 2)
        {
            return (activeMarked[0], activeMarked[1]);
        }

        var left = viewModel.LeftPane.CurrentItem;
        var right = viewModel.RightPane.CurrentItem;
        if (left is null || right is null || left.IsDirectory || right.IsDirectory)
        {
            return null;
        }

        return (left, right);
    }

    private async Task LaunchEditorAsync(MainWindowViewModel viewModel)
    {
        var current = viewModel.ActivePane.CurrentItem;
        if (current is null || current.IsDirectory)
        {
            return;
        }

        var settings = SettingsManager.Load();
        if (string.IsNullOrWhiteSpace(settings.ExternalEditor))
        {
            var dialog = new InputDialog(
                Strings.Dialog_Editor_Title,
                Strings.Dialog_Editor_Prompt);
            var editorPath = await dialog.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(editorPath))
            {
                return;
            }

            settings.ExternalEditor = editorPath;
            SettingsManager.Save(settings);
        }

        try
        {
            Process.Start(new ProcessStartInfo(settings.ExternalEditor!, current.FullPath)
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }
    }

    private async Task DuplicateCurrentItemAsync(MainWindowViewModel viewModel)
    {
        var current = viewModel.ActivePane.CurrentItem;
        if (current is null)
        {
            return;
        }

        var dialog = new RenameDialog(current.Name);
        var newName = await dialog.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(newName) || newName == current.Name)
        {
            return;
        }

        var parent = Path.GetDirectoryName(current.FullPath) ?? current.FullPath;
        var destPath = Path.Combine(parent, newName);

        try
        {
            if (current.IsDirectory)
            {
                CopyDirectoryRecursive(current.FullPath, destPath);
            }
            else
            {
                File.Copy(current.FullPath, destPath);
            }

            await viewModel.RefreshActivePaneAsync();
            viewModel.LogInfo(string.Format(Strings.Status_Duplicated, newName));
        }
        catch (Exception ex)
        {
            viewModel.LogError(ex.Message);
        }
    }

    private static void CopyDirectoryRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.EnumerateDirectories(source))
        {
            CopyDirectoryRecursive(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }
    }

    private static async Task<(string Text, bool IsTruncated)> ReadPreviewTextAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var bufferSize = (int)System.Math.Min(stream.Length, PreviewByteLimit);
        var buffer = new byte[bufferSize];
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead));
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        var text = Encoding.UTF8.GetString(buffer, 0, totalRead);
        return (text, stream.Length > PreviewByteLimit);
    }

    private void ScrollActiveSelectionIntoView(MainWindowViewModel viewModel)
    {
        var selectedItem = viewModel.ActivePane.SelectedItem;
        if (selectedItem is null)
        {
            return;
        }

        var grid = viewModel.LeftPane.IsActive ? LeftFileGrid : RightFileGrid;
        grid.ScrollIntoView(selectedItem, null);
        Focus();
    }

    private int GetVisibleFileRowCount(MainWindowViewModel viewModel)
    {
        var grid = viewModel.LeftPane.IsActive ? LeftFileGrid : RightFileGrid;
        const double rowHeight = 20;
        var headerHeight = grid.ColumnHeaderHeight;
        var availableHeight = Math.Max(0, grid.Bounds.Height - headerHeight);
        return Math.Max(1, (int)Math.Floor(availableHeight / rowHeight));
    }

    private void UpdatePaneVisualState()
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            UpdatePaneVisualState(viewModel);
        }
    }

    private void UpdatePaneVisualState(MainWindowViewModel viewModel)
    {
        SetPaneBorderState(LeftPaneBorder, viewModel.LeftPane.IsActive);
        SetPaneBorderState(RightPaneBorder, viewModel.RightPane.IsActive);
    }

    private static void SetPaneBorderState(Border border, bool isActive)
    {
        border.BorderBrush = isActive
            ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
            : new SolidColorBrush(Color.FromRgb(64, 64, 64));
        border.BorderThickness = new Thickness(2);
    }

    private static string NormalizeKeyWithModifiers(Key key, Avalonia.Input.KeyModifiers modifiers)
    {
        var keyStr = key switch
        {
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Space => "Space",
            Key.Escape => "Escape",
            Key.Oem2 => "/",
            Key.Divide => "/",
            _ => key.ToString(),
        };

        var parts = new List<string>();
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(Avalonia.Input.KeyModifiers.Meta)) parts.Add("Meta");
        parts.Add(keyStr);
        return string.Join("+", parts);
    }

    private bool TryResolveCommand(string key, out string commandId)
    {
        return keyToCommandMap.TryGetValue(key, out commandId!);
    }

    private static Dictionary<string, string> BuildKeyMap(IReadOnlyDictionary<string, string>? customKeyMap)
    {
        var map = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var binding in DefaultKeyMap.Bindings)
        {
            if (!IsMainWindowCommand(binding.CommandId))
            {
                continue;
            }

            map.TryAdd(NormalizeKeyName(binding.Gesture.ToString()), binding.CommandId);
        }

        if (customKeyMap is null)
        {
            return map;
        }

        foreach (var pair in customKeyMap)
        {
            if (!IsMainWindowCommand(pair.Key))
            {
                continue;
            }

            map[NormalizeKeyName(pair.Value)] = pair.Key;
        }

        return map;
    }

    private static bool IsMainWindowCommand(string commandId)
    {
        return commandId is not (
            ApplicationCommandId.DialogConfirm or
            ApplicationCommandId.DialogCancel or
            ApplicationCommandId.FilePreview);
    }

    private static bool ShouldRecordWorkingDirectory(string commandId)
    {
        return commandId is not (
            ApplicationCommandId.CursorUp or
            ApplicationCommandId.CursorDown or
            ApplicationCommandId.CursorLeft or
            ApplicationCommandId.CursorRight or
            ApplicationCommandId.CursorPageUp or
            ApplicationCommandId.CursorPageDown or
            ApplicationCommandId.CursorFirst or
            ApplicationCommandId.CursorLast or
            ApplicationCommandId.PaneSwitch or
            ApplicationCommandId.DirectoryOpen or
            ApplicationCommandId.DirectoryParent or
            ApplicationCommandId.DirectoryRoot or
            ApplicationCommandId.DriveChange or
            ApplicationCommandId.PaneSyncOpposite or
            ApplicationCommandId.DirectoryHistory);
    }

    private static string NormalizeKeyName(string key)
    {
        return key.Trim() switch
        {
            "Esc" => "Escape",
            "Back" => "Backspace",
            "Return" => "Enter",
            "Oem2" => "/",
            "OemQuestion" => "/",
            "Divide" => "/",
            var normalized => normalized,
        };
    }

    private static class NativeMethods
    {
        private const int SwShow = 5;
        private const uint SeeMaskInvokeIdList = 0x0000000C;

        public static bool ShowProperties(string path)
        {
            var info = new ShellExecuteInfo
            {
                cbSize = Marshal.SizeOf<ShellExecuteInfo>(),
                fMask = SeeMaskInvokeIdList,
                lpVerb = "properties",
                lpFile = path,
                nShow = SwShow,
            };

            return ShellExecuteEx(ref info);
        }

        [DllImport("shell32.dll", EntryPoint = "ShellExecuteExW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellExecuteEx(ref ShellExecuteInfo lpExecInfo);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShellExecuteInfo
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public string? lpVerb;
            public string? lpFile;
            public string? lpParameters;
            public string? lpDirectory;
            public int nShow;
            public IntPtr hInstApp;
            public IntPtr lpIDList;
            public string? lpClass;
            public IntPtr hkeyClass;
            public uint dwHotKey;
            public IntPtr hIcon;
            public IntPtr hProcess;
        }
    }
}
