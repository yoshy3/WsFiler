using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using WsFiler.Infra.Updates;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public enum UpdateAvailableAction
{
    UpgradeNow,
    UpgradeOnExit,
    Skip,
}

public sealed record UpdateAvailableDialogResult(UpdateAvailableAction Action, bool DisableUpdateCheck);

public partial class UpdateAvailableDialog : Window
{
    public UpdateAvailableDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        Title = Strings.Dialog_Update_Title;
        DisableUpdateCheckBox.Content = Strings.Dialog_Update_DisableCheck;
        UpgradeNowButton.Content = Strings.Dialog_Update_UpgradeNow;
        UpgradeOnExitButton.Content = Strings.Dialog_Update_UpgradeOnExit;
        SkipButton.Content = Strings.Dialog_Update_Skip;
    }

    public UpdateAvailableDialog(GitHubReleaseInfo release)
        : this()
    {
        MessageText.Text = string.Format(Strings.Dialog_Update_Message, release.Version, release.Name);
        Opened += (_, _) => UpgradeNowButton.Focus();
    }

    private UpdateAvailableDialogResult Result(UpdateAvailableAction action) =>
        new(action, DisableUpdateCheckBox.IsChecked == true);

    private void OnUpgradeNowClick(object? sender, RoutedEventArgs e)
    {
        Close(Result(UpdateAvailableAction.UpgradeNow));
    }

    private void OnUpgradeOnExitClick(object? sender, RoutedEventArgs e)
    {
        Close(Result(UpdateAvailableAction.UpgradeOnExit));
    }

    private void OnSkipClick(object? sender, RoutedEventArgs e)
    {
        Close(Result(UpdateAvailableAction.Skip));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
            case Key.Y:
                e.Handled = true;
                Close(Result(UpdateAvailableAction.UpgradeNow));
                break;
            case Key.D2:
            case Key.NumPad2:
            case Key.E:
                e.Handled = true;
                Close(Result(UpdateAvailableAction.UpgradeOnExit));
                break;
            case Key.D3:
            case Key.NumPad3:
            case Key.N:
            case Key.Escape:
                e.Handled = true;
                Close(Result(UpdateAvailableAction.Skip));
                break;
        }
    }
}
