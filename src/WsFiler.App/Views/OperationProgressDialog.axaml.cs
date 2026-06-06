using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using WsFiler.Core.Files;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class OperationProgressDialog : Window
{
    private readonly Action cancel = static () => { };

    public OperationProgressDialog()
    {
        InitializeComponent();
    }

    public OperationProgressDialog(string title, string initialMessage, Action cancel)
    {
        InitializeComponent();
        this.cancel = cancel;
        Title = title;
        MessageTextBlock.Text = initialMessage;
        CancelButton.Content = Strings.Dialog_Common_Cancel;
        ProgressBar.IsIndeterminate = true;
        CountTextBlock.Text = string.Empty;
    }

    public void Update(FileOperationProgress progress)
    {
        var name = Path.GetFileName(progress.CurrentPath);
        MessageTextBlock.Text = string.IsNullOrWhiteSpace(name)
            ? progress.CurrentPath
            : name;

        if (progress.TotalItems is { } total && total > 0)
        {
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Maximum = total;
            ProgressBar.Value = Math.Clamp(progress.CompletedItems, 0, total);
            CountTextBlock.Text = string.Format(
                Strings.Dialog_Progress_Count,
                progress.CompletedItems,
                total);
        }
        else
        {
            ProgressBar.IsIndeterminate = true;
            CountTextBlock.Text = string.Format(
                Strings.Dialog_Progress_CountUnknown,
                progress.CompletedItems);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        CountTextBlock.Text = Strings.Dialog_Progress_Canceling;
        cancel();
    }
}
