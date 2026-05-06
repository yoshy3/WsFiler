using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class InputDialog : Window
{
    public InputDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public InputDialog(string title, string prompt, string defaultValue = "")
        : this()
    {
        Title = title;
        PromptLabel.Text = prompt;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        InputTextBox.Text = defaultValue;
        Opened += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(InputTextBox.Text ?? "");
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
