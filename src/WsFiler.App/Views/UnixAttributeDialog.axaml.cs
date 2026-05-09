using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.IO;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class UnixAttributeDialog : Window
{
    public UnixAttributeDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public UnixAttributeDialog(string fileName, UnixFileMode current, bool canEdit)
        : this()
    {
        Title = Strings.Dialog_UnixAttributes_Title;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;

        FileNameLabel.Text = fileName;
        OwnerLabel.Text = Strings.Dialog_UnixAttributes_Owner;
        GroupLabel.Text = Strings.Dialog_UnixAttributes_Group;
        OtherLabel.Text = Strings.Dialog_UnixAttributes_Other;
        ReadLabel.Text = Strings.Dialog_UnixAttributes_Read;
        WriteLabel.Text = Strings.Dialog_UnixAttributes_Write;
        ExecuteLabel.Text = Strings.Dialog_UnixAttributes_Execute;
        SetUserIdCheckBox.Content = Strings.Dialog_UnixAttributes_SetUserId;
        SetGroupIdCheckBox.Content = Strings.Dialog_UnixAttributes_SetGroupId;
        StickyBitCheckBox.Content = Strings.Dialog_UnixAttributes_StickyBit;
        ReadOnlyMessage.Text = Strings.Dialog_UnixAttributes_ReadOnlyMessage;

        OwnerReadCheckBox.IsChecked = current.HasFlag(UnixFileMode.UserRead);
        OwnerWriteCheckBox.IsChecked = current.HasFlag(UnixFileMode.UserWrite);
        OwnerExecuteCheckBox.IsChecked = current.HasFlag(UnixFileMode.UserExecute);
        GroupReadCheckBox.IsChecked = current.HasFlag(UnixFileMode.GroupRead);
        GroupWriteCheckBox.IsChecked = current.HasFlag(UnixFileMode.GroupWrite);
        GroupExecuteCheckBox.IsChecked = current.HasFlag(UnixFileMode.GroupExecute);
        OtherReadCheckBox.IsChecked = current.HasFlag(UnixFileMode.OtherRead);
        OtherWriteCheckBox.IsChecked = current.HasFlag(UnixFileMode.OtherWrite);
        OtherExecuteCheckBox.IsChecked = current.HasFlag(UnixFileMode.OtherExecute);
        SetUserIdCheckBox.IsChecked = current.HasFlag(UnixFileMode.SetUser);
        SetGroupIdCheckBox.IsChecked = current.HasFlag(UnixFileMode.SetGroup);
        StickyBitCheckBox.IsChecked = current.HasFlag(UnixFileMode.StickyBit);

        if (!canEdit)
        {
            ReadOnlyMessage.IsVisible = true;
            OkButton.IsEnabled = false;
            foreach (var checkBox in PermissionCheckBoxes)
            {
                checkBox.IsEnabled = false;
            }
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var mode = (UnixFileMode)0;
        if (OwnerReadCheckBox.IsChecked == true) mode |= UnixFileMode.UserRead;
        if (OwnerWriteCheckBox.IsChecked == true) mode |= UnixFileMode.UserWrite;
        if (OwnerExecuteCheckBox.IsChecked == true) mode |= UnixFileMode.UserExecute;
        if (GroupReadCheckBox.IsChecked == true) mode |= UnixFileMode.GroupRead;
        if (GroupWriteCheckBox.IsChecked == true) mode |= UnixFileMode.GroupWrite;
        if (GroupExecuteCheckBox.IsChecked == true) mode |= UnixFileMode.GroupExecute;
        if (OtherReadCheckBox.IsChecked == true) mode |= UnixFileMode.OtherRead;
        if (OtherWriteCheckBox.IsChecked == true) mode |= UnixFileMode.OtherWrite;
        if (OtherExecuteCheckBox.IsChecked == true) mode |= UnixFileMode.OtherExecute;
        if (SetUserIdCheckBox.IsChecked == true) mode |= UnixFileMode.SetUser;
        if (SetGroupIdCheckBox.IsChecked == true) mode |= UnixFileMode.SetGroup;
        if (StickyBitCheckBox.IsChecked == true) mode |= UnixFileMode.StickyBit;
        Close((UnixFileMode?)mode);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

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

    private CheckBox[] PermissionCheckBoxes =>
    [
        OwnerReadCheckBox,
        OwnerWriteCheckBox,
        OwnerExecuteCheckBox,
        GroupReadCheckBox,
        GroupWriteCheckBox,
        GroupExecuteCheckBox,
        OtherReadCheckBox,
        OtherWriteCheckBox,
        OtherExecuteCheckBox,
        SetUserIdCheckBox,
        SetGroupIdCheckBox,
        StickyBitCheckBox,
    ];
}
