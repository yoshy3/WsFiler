using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public sealed record DirectoryBookmarkDialogResult(
    IReadOnlyList<string> Bookmarks,
    string? JumpPath);

public partial class DirectoryBookmarkDialog : Window
{
    private readonly string currentPath;
    private readonly ObservableCollection<string> bookmarks;
    private bool isClosingWithResult;

    public DirectoryBookmarkDialog()
        : this("", [])
    {
    }

    public DirectoryBookmarkDialog(string currentPath, IReadOnlyList<string> initialBookmarks)
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        this.currentPath = currentPath;
        bookmarks = new ObservableCollection<string>(
            initialBookmarks
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        Title = Strings.Dialog_Bookmark_Title;
        CurrentPathLabel.Text = Strings.Dialog_Bookmark_CurrentPath;
        CurrentPathTextBlock.Text = currentPath;
        AddButton.Content = Strings.Dialog_Bookmark_Add;
        EditButton.Content = Strings.Dialog_Bookmark_Edit;
        DeleteButton.Content = Strings.Dialog_Bookmark_Delete;
        JumpButton.Content = Strings.Dialog_Bookmark_Jump;
        CloseButton.Content = Strings.Dialog_Common_Close;
        BookmarkListBox.ItemsSource = bookmarks;

        if (bookmarks.Count > 0)
        {
            BookmarkListBox.SelectedIndex = 0;
        }

        Opened += (_, _) => Dispatcher.UIThread.Post(() => BookmarkListBox.Focus());
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        AddCurrentPath();
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        await EditSelectedBookmarkAsync();
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        DeleteSelectedBookmark();
    }

    private void OnJumpClick(object? sender, RoutedEventArgs e)
    {
        CloseWithSelectedBookmark();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        CloseWithoutJump();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        CloseWithSelectedBookmark();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseWithoutJump();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            HandleEnter();
        }
        else if (e.Key == Key.Delete)
        {
            e.Handled = true;
            DeleteSelectedBookmark();
        }
        else if (e.Key == Key.E)
        {
            e.Handled = true;
            _ = EditSelectedBookmarkAsync();
        }
        else if (e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            MoveSelection(e.Key == Key.Down ? 1 : -1);
        }
        else if (e.Key == Key.Insert || e.Key == Key.A)
        {
            e.Handled = true;
            AddCurrentPath();
        }
    }

    private void AddCurrentPath()
    {
        if (string.IsNullOrWhiteSpace(currentPath) ||
            bookmarks.Contains(currentPath, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        bookmarks.Add(currentPath);
        BookmarkListBox.SelectedItem = currentPath;
        BookmarkListBox.ScrollIntoView(currentPath);
        BookmarkListBox.Focus();
    }

    private void HandleEnter()
    {
        if (AddButton.IsFocused)
        {
            AddCurrentPath();
        }
        else if (DeleteButton.IsFocused)
        {
            DeleteSelectedBookmark();
        }
        else if (EditButton.IsFocused)
        {
            _ = EditSelectedBookmarkAsync();
        }
        else if (JumpButton.IsFocused || BookmarkListBox.IsFocused)
        {
            CloseWithSelectedBookmark();
        }
        else if (CloseButton.IsFocused)
        {
            CloseWithoutJump();
        }
        else
        {
            CloseWithSelectedBookmark();
        }
    }

    private void DeleteSelectedBookmark()
    {
        if (BookmarkListBox.SelectedItem is not string selected)
        {
            return;
        }

        var index = BookmarkListBox.SelectedIndex;
        bookmarks.Remove(selected);
        if (bookmarks.Count > 0)
        {
            BookmarkListBox.SelectedIndex = Math.Clamp(index, 0, bookmarks.Count - 1);
        }

        BookmarkListBox.Focus();
    }

    private async Task EditSelectedBookmarkAsync()
    {
        if (BookmarkListBox.SelectedItem is not string selected)
        {
            return;
        }

        var dialog = new InputDialog(
            Strings.Dialog_Bookmark_Title,
            Strings.Dialog_Bookmark_Path,
            selected);
        var edited = await dialog.ShowDialog<string?>(this);
        var path = edited?.Trim();
        if (string.IsNullOrWhiteSpace(path) ||
            string.Equals(path, selected, StringComparison.OrdinalIgnoreCase) ||
            bookmarks.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            BookmarkListBox.Focus();
            return;
        }

        var index = BookmarkListBox.SelectedIndex;
        bookmarks[index] = path;
        BookmarkListBox.SelectedIndex = index;
        BookmarkListBox.ScrollIntoView(path);
        BookmarkListBox.Focus();
    }

    private void MoveSelection(int offset)
    {
        if (bookmarks.Count == 0)
        {
            return;
        }

        var currentIndex = BookmarkListBox.SelectedIndex >= 0 ? BookmarkListBox.SelectedIndex : 0;
        var nextIndex = Math.Clamp(currentIndex + offset, 0, bookmarks.Count - 1);
        BookmarkListBox.SelectedIndex = nextIndex;
        BookmarkListBox.Focus();

        if (BookmarkListBox.SelectedItem is { } selectedItem)
        {
            BookmarkListBox.ScrollIntoView(selectedItem);
        }
    }

    private void CloseWithSelectedBookmark()
    {
        if (BookmarkListBox.SelectedItem is string selected)
        {
            CloseWithResult(selected);
        }
    }

    private void CloseWithoutJump()
    {
        CloseWithResult(null);
    }

    private void CloseWithResult(string? jumpPath)
    {
        isClosingWithResult = true;
        Close(new DirectoryBookmarkDialogResult(bookmarks.ToList(), jumpPath));
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!isClosingWithResult)
        {
            e.Cancel = true;
            CloseWithoutJump();
            return;
        }

        base.OnClosing(e);
    }
}
