using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class RenameDialog : Window
{
    public RenameDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public RenameDialog(string currentName)
        : this()
    {
        Title = Strings.Dialog_Rename_Title;
        NewNameLabel.Text = Strings.Dialog_Rename_NewName;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        NameTextBox.Text = currentName;
        Opened += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(NameTextBox.Text ?? "");
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }
}
