using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WsFiler.App.Views;

public partial class CopyConfirmDialog : Window
{
    public CopyConfirmDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public CopyConfirmDialog(string representativeName, int targetCount, string destinationDirectory)
        : this()
    {
        MessageText.Text = targetCount == 1
            ? $"{representativeName} をコピーします。"
            : $"{representativeName} ほか {targetCount - 1:N0} 件をコピーします。";
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
