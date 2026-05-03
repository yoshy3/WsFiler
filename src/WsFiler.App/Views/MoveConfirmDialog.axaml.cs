using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WsFiler.App.Views;

public partial class MoveConfirmDialog : Window
{
    public MoveConfirmDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public MoveConfirmDialog(string representativeName, int targetCount, string destinationDirectory)
        : this()
    {
        MessageText.Text = targetCount == 1
            ? $"{representativeName} を移動します。"
            : $"{representativeName} ほか {targetCount - 1:N0} 件を移動します。";
        DestinationTextBox.Text = destinationDirectory;
        Opened += (_, _) => YesButton.Focus();
    }

    private void OnYesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnNoClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Y:
                e.Handled = true;
                Close(true);
                break;
            case Key.N:
            case Key.Escape:
                e.Handled = true;
                Close(false);
                break;
        }
    }
}
