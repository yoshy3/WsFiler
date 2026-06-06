using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Core.Files;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class ReadOnlyDeleteConfirmDialog : Window
{
    public ReadOnlyDeleteConfirmDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Title = Strings.Dialog_ReadOnlyDelete_Title;
        ApplyToAllCheckBox.Content = Strings.Dialog_Conflict_ApplyToAll;
        DeleteButton.Content = Strings.Dialog_ReadOnlyDelete_Delete;
        SkipButton.Content = Strings.Dialog_Conflict_Skip;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
    }

    public ReadOnlyDeleteConfirmDialog(FileDeleteConfirmationInfo info)
        : this()
    {
        MessageText.Text = string.Format(Strings.Dialog_ReadOnlyDelete_Message, info.ItemName);
        Opened += (_, _) => DeleteButton.Focus();
    }

    private FileDeleteConfirmationDecision Decision(FileDeleteConfirmationAction action)
    {
        return new FileDeleteConfirmationDecision(action, ApplyToAllCheckBox.IsChecked == true);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Close(Decision(FileDeleteConfirmationAction.Delete));
    }

    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        Close(Decision(FileDeleteConfirmationAction.Skip));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(Decision(FileDeleteConfirmationAction.Cancel));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Y:
                e.Handled = true;
                Close(Decision(FileDeleteConfirmationAction.Delete));
                break;
            case Key.N:
                e.Handled = true;
                Close(Decision(FileDeleteConfirmationAction.Skip));
                break;
            case Key.Escape:
                e.Handled = true;
                Close(Decision(FileDeleteConfirmationAction.Cancel));
                break;
        }
    }
}
