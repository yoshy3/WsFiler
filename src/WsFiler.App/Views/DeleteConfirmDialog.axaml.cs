using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class DeleteConfirmDialog : Window
{
    public DeleteConfirmDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Title = Strings.Dialog_Delete_Title;
        YesButton.Content = Strings.Dialog_Common_Yes;
        NoButton.Content = Strings.Dialog_Common_No;
    }

    public DeleteConfirmDialog(string representativeName, int targetCount)
        : this()
    {
        MessageText.Text = targetCount == 1
            ? string.Format(Strings.Dialog_Delete_MessageSingle, representativeName)
            : string.Format(Strings.Dialog_Delete_MessageMultiple, representativeName, targetCount - 1);
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
