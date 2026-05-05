using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Collections.Generic;
using System.Threading.Tasks;
using WsFiler.Core.Commands;
using WsFiler.Core.Files;
using WsFiler.Core.KeyMap;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App.Views;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, string> keyToCommandMap;

    public MainWindow(IReadOnlyDictionary<string, string>? customKeyMap = null)
    {
        InitializeComponent();
        keyToCommandMap = BuildKeyMap(customKeyMap);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += (_, _) => UpdatePaneVisualState();
        Focusable = true;
    }

    public MainWindow()
    {
        InitializeComponent();
        keyToCommandMap = BuildKeyMap(null);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        DataContextChanged += (_, _) => UpdatePaneVisualState();
        Focusable = true;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var key = NormalizeKey(e.Key);
        if (!TryResolveCommand(key, out var commandId))
        {
            return;
        }

        e.Handled = true;

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
        else
        {
            await viewModel.HandleCommandAsync(commandId);
        }

        ScrollActiveSelectionIntoView(viewModel);
        UpdatePaneVisualState(viewModel);
    }

    private void OnLeftFileGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            LeftFileGrid.SelectedItem is not FileItemViewModel selectedItem)
        {
            return;
        }

        viewModel.ActivateLeftPane(selectedItem);
        UpdatePaneVisualState(viewModel);
        LeftFileGrid.Focus();
    }

    private void OnRightFileGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            RightFileGrid.SelectedItem is not FileItemViewModel selectedItem)
        {
            return;
        }

        viewModel.ActivateRightPane(selectedItem);
        UpdatePaneVisualState(viewModel);
        RightFileGrid.Focus();
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

    private void ScrollActiveSelectionIntoView(MainWindowViewModel viewModel)
    {
        var selectedItem = viewModel.ActivePane.SelectedItem;
        if (selectedItem is null)
        {
            return;
        }

        var grid = viewModel.LeftPane.IsActive ? LeftFileGrid : RightFileGrid;
        grid.ScrollIntoView(selectedItem, null);
        grid.Focus();
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
        border.BorderThickness = isActive ? new Thickness(2) : new Thickness(1);
    }

    private static string NormalizeKey(Key key)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Back => "Backspace",
            Key.Space => "Space",
            Key.Escape => "Escape",
            Key.C => "C",
            Key.M => "M",
            Key.D => "D",
            Key.R => "R",
            _ => key.ToString(),
        };
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

            map.TryAdd(NormalizeKeyName(binding.Gesture.Key), binding.CommandId);
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

    private static string NormalizeKeyName(string key)
    {
        return key.Trim() switch
        {
            "Esc" => "Escape",
            "Back" => "Backspace",
            "Return" => "Enter",
            var normalized => normalized,
        };
    }
}
