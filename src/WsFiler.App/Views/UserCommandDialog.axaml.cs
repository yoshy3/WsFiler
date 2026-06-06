using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WsFiler.Infra.Settings;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class UserCommandDialog : Window
{
    private readonly HashSet<string> existingNames;
    private readonly string? originalName;

    public UserCommandDialog()
        : this(null, [])
    {
    }

    public UserCommandDialog(UserCommandEntry? command, IEnumerable<string> existingNames)
    {
        InitializeComponent();

        originalName = command?.Name;
        this.existingNames = existingNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Title = command is null
            ? Strings.Dialog_UserCommand_AddTitle
            : Strings.Dialog_UserCommand_EditTitle;
        NameLabel.Text = Strings.Dialog_UserCommand_Name;
        ExecutableLabel.Text = Strings.Dialog_UserCommand_ExecutablePath;
        ArgumentsLabel.Text = Strings.Dialog_UserCommand_Arguments;
        WorkingDirectoryLabel.Text = Strings.Dialog_UserCommand_WorkingDirectory;
        WorkingDirectoryComboBox.ItemsSource = new[]
        {
            new WorkingDirectoryModeItem(
                UserCommandEntry.WorkingDirectoryCurrent,
                Strings.Dialog_UserCommand_WorkingDirectoryCurrent),
            new WorkingDirectoryModeItem(
                UserCommandEntry.WorkingDirectoryExecutable,
                Strings.Dialog_UserCommand_WorkingDirectoryExecutable),
        };
        MacroHelpLabel.Text = Strings.Dialog_UserCommand_MacroHelp;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        ErrorLabel.Text = "";

        NameTextBox.Text = command?.Name ?? "";
        ExecutableTextBox.Text = command?.ExecutablePath ?? "";
        ArgumentsTextBox.Text = command?.Arguments ?? "";
        WorkingDirectoryComboBox.SelectedIndex = string.Equals(
            command?.WorkingDirectoryMode,
            UserCommandEntry.WorkingDirectoryExecutable,
            StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text?.Trim() ?? "";
        var executablePath = ExecutableTextBox.Text?.Trim() ?? "";
        var arguments = ArgumentsTextBox.Text ?? "";
        var workingDirectoryMode = WorkingDirectoryComboBox.SelectedItem is WorkingDirectoryModeItem selectedMode
            ? selectedMode.Key
            : UserCommandEntry.WorkingDirectoryCurrent;

        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorLabel.Text = Strings.Dialog_UserCommand_ErrorNameRequired;
            return;
        }

        if (!string.Equals(name, originalName, StringComparison.OrdinalIgnoreCase) &&
            existingNames.Contains(name))
        {
            ErrorLabel.Text = Strings.Dialog_UserCommand_ErrorNameDuplicate;
            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            ErrorLabel.Text = Strings.Dialog_UserCommand_ErrorExecutableRequired;
            return;
        }

        if (!File.Exists(executablePath))
        {
            ErrorLabel.Text = Strings.Dialog_UserCommand_ErrorExecutableNotFound;
            return;
        }

        Close(new UserCommandEntry
        {
            Name = name,
            ExecutablePath = executablePath,
            Arguments = arguments,
            WorkingDirectoryMode = workingDirectoryMode,
        });
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private sealed record WorkingDirectoryModeItem(string Key, string Display)
    {
        public override string ToString() => Display;
    }
}
