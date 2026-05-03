using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;
using WsFiler.Presentation.ViewModels;

namespace WsFiler.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Focusable = true;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var key = NormalizeKey(e.Key);
        if (!IsHandledKey(key))
        {
            return;
        }

        e.Handled = true;

        if (key == "C")
        {
            await ConfirmAndCopyAsync(viewModel);
        }
        else if (key == "M")
        {
            await ConfirmAndMoveAsync(viewModel);
        }
        else if (key == "D")
        {
            await ConfirmAndDeleteAsync(viewModel);
        }
        else if (key == "R")
        {
            await ConfirmAndRenameAsync(viewModel);
        }
        else
        {
            await viewModel.HandleKeyAsync(key);
        }

        ScrollActiveSelectionIntoView(viewModel);
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
            await viewModel.CopyAsync(request);
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
            await viewModel.MoveAsync(request);
        }
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

    private static string NormalizeKey(Key key)
    {
        return key switch
        {
            Key.Return => "Enter",
            Key.Back => "Back",
            Key.Space => "Space",
            Key.C => "C",
            Key.M => "M",
            Key.D => "D",
            Key.R => "R",
            _ => key.ToString(),
        };
    }

    private static bool IsHandledKey(string key)
    {
        return key is "Up" or "Down" or "Tab" or "Left" or "Right" or "Enter" or "Back" or "Space" or "C" or "M" or "D" or "R";
    }
}
