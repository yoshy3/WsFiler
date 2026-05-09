using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public sealed record FilterDialogResult(string Pattern, bool ShowHiddenFiles);

public partial class FilterDialog : Window
{
    public FilterDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public FilterDialog(bool showHiddenFiles)
        : this()
    {
        Title = Strings.Dialog_Filter_Title;
        PromptLabel.Text = Strings.Dialog_Filter_Prompt;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        ShowHiddenCheckBox.Content = Strings.Dialog_Filter_ShowHidden;
        InputTextBox.Text = "";
        ShowHiddenCheckBox.IsChecked = showHiddenFiles;
        Opened += (_, _) =>
        {
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close(new FilterDialogResult(
            InputTextBox.Text ?? "",
            ShowHiddenCheckBox.IsChecked == true));
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
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            e.Handled = true;
            OnOkClick(this, new RoutedEventArgs());
        }
    }
}
