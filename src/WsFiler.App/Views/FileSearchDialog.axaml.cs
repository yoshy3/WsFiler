using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WsFiler.App;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class FileSearchDialog : Window
{
    private readonly List<SearchResult> results = [];
    private CancellationTokenSource? searchCancellationTokenSource;

    public FileSearchDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public FileSearchDialog(string baseDirectory, string defaultPattern = "")
        : this()
    {
        Title = Strings.Dialog_FileSearch_Title;
        PatternLabel.Text = Strings.Dialog_FileSearch_FileName;
        BaseDirectoryLabel.Text = Strings.Dialog_FileSearch_BaseDirectory;
        SearchDirectoriesCheckBox.Content = Strings.Dialog_FileSearch_SearchDirectories;
        SearchButton.Content = Strings.Dialog_FileSearch_Start;
        CancelSearchButton.Content = Strings.Dialog_Common_Cancel;
        JumpButton.Content = Strings.Dialog_FileSearch_Jump;
        CloseButton.Content = Strings.Dialog_Common_Cancel;
        ResultLabel.Text = Strings.Dialog_FileSearch_Results;
        BaseDirectoryTextBox.Text = baseDirectory;
        PatternTextBox.Text = defaultPattern;
        UpdateResultCount();

        Opened += (_, _) =>
        {
            PatternTextBox.Focus();
            PatternTextBox.SelectAll();
        };
    }

    private async void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private void OnJumpClick(object? sender, RoutedEventArgs e)
    {
        CloseWithSelection();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (searchCancellationTokenSource is not null)
        {
            searchCancellationTokenSource.Cancel();
            return;
        }

        Close(null);
    }

    private void OnCancelSearchClick(object? sender, RoutedEventArgs e)
    {
        searchCancellationTokenSource?.Cancel();
    }

    private void OnResultDoubleTapped(object? sender, TappedEventArgs e)
    {
        CloseWithSelection();
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (searchCancellationTokenSource is not null)
            {
                searchCancellationTokenSource.Cancel();
                return;
            }

            Close(null);
        }
        else if (e.Key == Key.Enter && ResultListBox.IsFocused)
        {
            e.Handled = true;
            CloseWithSelection();
        }
        else if (results.Count > 0 && e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            MoveResultSelection(e.Key == Key.Down ? 1 : -1);
        }
        else if (results.Count > 0 && e.Key == Key.Enter)
        {
            e.Handled = true;
            CloseWithSelection();
        }
        else if (e.Key == Key.Enter && PatternTextBox.IsFocused)
        {
            e.Handled = true;
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        var baseDirectory = BaseDirectoryTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            ResultCountTextBlock.Text = Strings.Dialog_FileSearch_BaseDirectoryNotFound;
            return;
        }

        var pattern = PatternTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "*";
        }

        SearchButton.IsEnabled = false;
        CancelSearchButton.IsEnabled = true;
        SearchProgressBar.IsVisible = true;
        SearchProgressTextBlock.Text = Strings.Dialog_FileSearch_Searching;
        ResultListBox.ItemsSource = null;
        results.Clear();
        UpdateResultCount();

        using var cancellationTokenSource = new CancellationTokenSource();
        searchCancellationTokenSource = cancellationTokenSource;

        try
        {
            var includeDirectories = SearchDirectoriesCheckBox.IsChecked == true;
            var matcher = BuildMatcher(pattern);
            var progress = new ThrottledProgress<SearchProgress>(
                new Progress<SearchProgress>(UpdateSearchProgress),
                TimeSpan.FromMilliseconds(50));
            var found = await Task.Run(
                () => FindMatches(
                    baseDirectory,
                    matcher,
                    includeDirectories,
                    progress,
                    cancellationTokenSource.Token),
                cancellationTokenSource.Token);
            progress.Flush();
            results.AddRange(found);
            ResultListBox.ItemsSource = results;
            if (results.Count > 0)
            {
                ResultListBox.SelectedIndex = 0;
                ResultListBox.Focus();
            }

            UpdateResultCount();
        }
        catch (OperationCanceledException)
        {
            SearchProgressTextBlock.Text = Strings.Dialog_Progress_Canceled;
            UpdateResultCount();
        }
        finally
        {
            searchCancellationTokenSource = null;
            SearchButton.IsEnabled = true;
            CancelSearchButton.IsEnabled = false;
            SearchProgressBar.IsVisible = false;
        }
    }

    private static List<SearchResult> FindMatches(
        string baseDirectory,
        Regex matcher,
        bool includeDirectories,
        IProgress<SearchProgress> progress,
        CancellationToken cancellationToken)
    {
        var found = new List<SearchResult>();
        var pending = new Stack<string>();
        var scannedDirectories = 0;
        pending.Push(baseDirectory);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            scannedDirectories++;
            progress.Report(new SearchProgress(directory, found.Count, scannedDirectories));
            IEnumerable<string> childDirectories;

            try
            {
                childDirectories = Directory.EnumerateDirectories(directory).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (includeDirectories && matcher.IsMatch(Path.GetFileName(childDirectory)))
                {
                    found.Add(new SearchResult(childDirectory, baseDirectory));
                }

                pending.Push(childDirectory);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (matcher.IsMatch(Path.GetFileName(file)))
                {
                    found.Add(new SearchResult(file, baseDirectory));
                }
            }
        }

        progress.Report(new SearchProgress(baseDirectory, found.Count, scannedDirectories));
        return found
            .OrderBy(result => result.DisplayPath, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static Regex BuildMatcher(string pattern)
    {
        if (!pattern.Contains('*') && !pattern.Contains('?'))
        {
            pattern = $"*{pattern}*";
        }

        var escaped = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private void UpdateResultCount()
    {
        ResultCountTextBlock.Text = string.Format(Strings.Dialog_FileSearch_Count, results.Count);
    }

    private void UpdateSearchProgress(SearchProgress progress)
    {
        ResultCountTextBlock.Text = string.Format(Strings.Dialog_FileSearch_Count, progress.FoundCount);
        SearchProgressTextBlock.Text = string.Format(
            Strings.Dialog_FileSearch_Progress,
            progress.ScannedDirectories,
            progress.CurrentDirectory);
    }

    private void CloseWithSelection()
    {
        if (ResultListBox.SelectedItem is SearchResult result)
        {
            Close(result.FullPath);
        }
    }

    private void MoveResultSelection(int offset)
    {
        var currentIndex = ResultListBox.SelectedIndex >= 0 ? ResultListBox.SelectedIndex : 0;
        var nextIndex = Math.Clamp(currentIndex + offset, 0, results.Count - 1);
        ResultListBox.SelectedIndex = nextIndex;
        ResultListBox.Focus();

        if (ResultListBox.SelectedItem is { } selectedItem)
        {
            ResultListBox.ScrollIntoView(selectedItem);
        }
    }

    private sealed class SearchResult(string fullPath, string baseDirectory)
    {
        public string FullPath { get; } = fullPath;

        public string DisplayPath { get; } = Path.GetRelativePath(baseDirectory, fullPath);

        public override string ToString() => DisplayPath;
    }

    private sealed record SearchProgress(
        string CurrentDirectory,
        int FoundCount,
        int ScannedDirectories);
}
