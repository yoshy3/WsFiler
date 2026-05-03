using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        await viewModel.HandleKeyAsync(key);
        ScrollActiveSelectionIntoView(viewModel);
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
            _ => key.ToString(),
        };
    }

    private static bool IsHandledKey(string key)
    {
        return key is "Up" or "Down" or "Tab" or "Left" or "Right" or "Enter" or "Back" or "Space";
    }
}
