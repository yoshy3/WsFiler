using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Core.Files;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class ConflictConfirmDialog : Window
{
    public ConflictConfirmDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Title = Strings.Dialog_Conflict_Title;
        ApplyToAllCheckBox.Content = Strings.Dialog_Conflict_ApplyToAll;
        OverwriteButton.Content = Strings.Dialog_Conflict_Overwrite;
        SkipButton.Content = Strings.Dialog_Conflict_Skip;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
    }

    public ConflictConfirmDialog(FileConflictInfo conflict)
        : this()
    {
        MessageText.Text = string.Format(Strings.Dialog_Conflict_Message, conflict.ItemName);
        Opened += (_, _) => OverwriteButton.Focus();
    }

    public FileConflictDecision Decision(FileConflictAction action)
    {
        return new FileConflictDecision(action, ApplyToAllCheckBox.IsChecked == true);
    }

    private void OnOverwriteClick(object? sender, RoutedEventArgs e)
    {
        Close(Decision(FileConflictAction.Overwrite));
    }

    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        Close(Decision(FileConflictAction.Skip));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(Decision(FileConflictAction.Cancel));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Y:
                e.Handled = true;
                Close(Decision(FileConflictAction.Overwrite));
                break;
            case Key.N:
                e.Handled = true;
                Close(Decision(FileConflictAction.Skip));
                break;
            case Key.Escape:
                e.Handled = true;
                Close(Decision(FileConflictAction.Cancel));
                break;
        }
    }
}
