using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.IO;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class AttributeDialog : Window
{
    public AttributeDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public AttributeDialog(string fileName, FileAttributes current)
        : this()
    {
        Title = Strings.Dialog_Attributes_Title;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;

        FileNameLabel.Text = fileName;
        ReadOnlyCheckBox.Content = Strings.Dialog_Attributes_ReadOnly;
        HiddenCheckBox.Content = Strings.Dialog_Attributes_Hidden;
        ArchiveCheckBox.Content = Strings.Dialog_Attributes_Archive;
        SystemCheckBox.Content = Strings.Dialog_Attributes_System;

        ReadOnlyCheckBox.IsChecked = current.HasFlag(FileAttributes.ReadOnly);
        HiddenCheckBox.IsChecked = current.HasFlag(FileAttributes.Hidden);
        ArchiveCheckBox.IsChecked = current.HasFlag(FileAttributes.Archive);
        SystemCheckBox.IsChecked = current.HasFlag(FileAttributes.System);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var attrs = FileAttributes.Normal;
        if (ReadOnlyCheckBox.IsChecked == true) attrs |= FileAttributes.ReadOnly;
        if (HiddenCheckBox.IsChecked == true) attrs |= FileAttributes.Hidden;
        if (ArchiveCheckBox.IsChecked == true) attrs |= FileAttributes.Archive;
        if (SystemCheckBox.IsChecked == true) attrs |= FileAttributes.System;
        Close((FileAttributes?)attrs);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }
}
