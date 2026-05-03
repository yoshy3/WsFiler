using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace WsFiler.App.Views;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public DeleteConfirmDialog(string representativeName, int targetCount)
        : this()
    {
        MessageText.Text = targetCount == 1
            ? $"{representativeName} を完全に削除します。"
            : $"{representativeName} ほか {targetCount - 1:N0} 件を完全に削除します。";
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
