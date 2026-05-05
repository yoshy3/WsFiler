using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Presentation.Resources;

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
        Title = Strings.Dialog_Move_Title;
        MessageText.Text = targetCount == 1
            ? string.Format(Strings.Dialog_Move_MessageSingle, representativeName)
            : string.Format(Strings.Dialog_Move_MessageMultiple, representativeName, targetCount - 1);
        DestinationLabel.Text = Strings.Dialog_Move_Destination;
        YesButton.Content = Strings.Dialog_Common_Yes;
        NoButton.Content = Strings.Dialog_Common_No;
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
