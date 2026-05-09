using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class DirectoryHistoryDialog : Window
{
    private readonly IReadOnlyList<string> history;

    public DirectoryHistoryDialog()
        : this([])
    {
    }

    public DirectoryHistoryDialog(IReadOnlyList<string> history)
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        this.history = history
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Title = Strings.Dialog_History_Title;
        HistoryLabel.Text = Strings.Dialog_History_Title;
        JumpButton.Content = Strings.Dialog_Bookmark_Jump;
        CloseButton.Content = Strings.Dialog_Common_Cancel;
        HistoryListBox.ItemsSource = this.history;

        if (this.history.Count > 0)
        {
            HistoryListBox.SelectedIndex = 0;
        }

        Opened += (_, _) => Dispatcher.UIThread.Post(() => HistoryListBox.Focus());
    }

    private void OnJumpClick(object? sender, RoutedEventArgs e)
    {
        CloseWithSelection();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        CloseWithSelection();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (CloseButton.IsFocused)
            {
                Close(null);
            }
            else
            {
                CloseWithSelection();
            }
        }
        else if (e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            MoveSelection(e.Key == Key.Down ? 1 : -1);
        }
    }

    private void MoveSelection(int offset)
    {
        if (history.Count == 0)
        {
            return;
        }

        var currentIndex = HistoryListBox.SelectedIndex >= 0 ? HistoryListBox.SelectedIndex : 0;
        var nextIndex = Math.Clamp(currentIndex + offset, 0, history.Count - 1);
        HistoryListBox.SelectedIndex = nextIndex;
        HistoryListBox.Focus();

        if (HistoryListBox.SelectedItem is { } selectedItem)
        {
            HistoryListBox.ScrollIntoView(selectedItem);
        }
    }

    private void CloseWithSelection()
    {
        if (HistoryListBox.SelectedItem is string selected)
        {
            Close(selected);
        }
    }
}
