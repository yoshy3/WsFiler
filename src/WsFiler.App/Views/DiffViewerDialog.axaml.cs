using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class DiffViewerDialog : Window
{
    private static readonly IBrush EqualBackground = new SolidColorBrush(Color.FromRgb(30, 30, 30));
    private static readonly IBrush ChangedBackground = new SolidColorBrush(Color.FromRgb(74, 0, 0));
    private static readonly IBrush InsertedBackground = new SolidColorBrush(Color.FromRgb(0, 58, 20));
    private static readonly IBrush DeletedBackground = new SolidColorBrush(Color.FromRgb(55, 55, 55));
    private static readonly IBrush MissingBackground = new SolidColorBrush(Color.FromRgb(70, 70, 70));
    private static readonly IBrush CharacterDiffBackground = new SolidColorBrush(Color.FromRgb(0, 38, 120));
    private static readonly IBrush CharacterDiffTextBrush = new SolidColorBrush(Color.FromRgb(0, 255, 255));
    private static readonly IBrush TextBrush = new SolidColorBrush(Color.FromRgb(235, 235, 235));
    private static readonly IBrush MutedTextBrush = new SolidColorBrush(Color.FromRgb(150, 150, 150));

    private IReadOnlyList<DiffRow> rows = [];

    public DiffViewerDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DifferencesOnlyCheckBox.Content = Strings.Dialog_Diff_DifferencesOnly;
    }

    public static async Task<DiffViewerDialog> CreateAsync(string leftPath, string rightPath)
    {
        var dialog = new DiffViewerDialog();
        await dialog.LoadAsync(leftPath, rightPath);
        return dialog;
    }

    public void FitToOwner(Window owner)
    {
        Width = Math.Max(MinWidth, owner.Bounds.Width * 0.94);
        Height = Math.Max(MinHeight, owner.Bounds.Height * 0.9);
    }

    private async Task LoadAsync(string leftPath, string rightPath)
    {
        Title = Strings.Dialog_Diff_Title;
        LeftPathTextBlock.Text = leftPath;
        RightPathTextBlock.Text = rightPath;

        var leftLines = await File.ReadAllLinesAsync(leftPath);
        var rightLines = await File.ReadAllLinesAsync(rightPath);
        rows = BuildLineDiff(leftLines, rightLines);
        RenderRows();
    }

    private void RenderRows()
    {
        RowsPanel.Children.Clear();

        var visibleRows = DifferencesOnlyCheckBox.IsChecked == true
            ? rows.Where(row => row.Kind != DiffKind.Equal)
            : rows;

        foreach (var row in visibleRows)
        {
            RowsPanel.Children.Add(CreateRow(row));
        }

        RowsScrollViewer.Offset = new Vector(0, 0);
    }

    private void OnDifferencesOnlyChanged(object? sender, RoutedEventArgs e)
    {
        RenderRows();
    }

    private Control CreateRow(DiffRow row)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            ClipToBounds = true,
        };

        grid.Children.Add(CreateSide(row.LeftLineNumber, row.LeftText, row.RightText, row.Kind, isLeft: true, 0));
        grid.Children.Add(CreateSide(row.RightLineNumber, row.RightText, row.LeftText, row.Kind, isLeft: false, 1));
        return grid;
    }

    private static Grid CreateSide(
        int? lineNumber,
        string? text,
        string? oppositeText,
        DiffKind kind,
        bool isLeft,
        int column)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("48,*"),
            ClipToBounds = true,
        };

        grid.Children.Add(CreateLineNumber(lineNumber, 0));
        grid.Children.Add(CreateTextCell(text, oppositeText, kind, isLeft, 1));

        Grid.SetColumn(grid, column);
        return grid;
    }

    private static TextBlock CreateLineNumber(int? lineNumber, int column)
    {
        var textBlock = new TextBlock
        {
            Text = lineNumber?.ToString("N0") ?? "",
            FontFamily = FontFamily.Parse("Consolas"),
            FontSize = 13,
            Foreground = MutedTextBrush,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0),
        };
        Grid.SetColumn(textBlock, column);
        return textBlock;
    }

    private static Border CreateTextCell(
        string? text,
        string? oppositeText,
        DiffKind kind,
        bool isLeft,
        int column)
    {
        var textBlock = new TextBlock
        {
            FontFamily = FontFamily.Parse("Consolas"),
            FontSize = 13,
            Foreground = text is null ? MutedTextBrush : TextBrush,
            TextWrapping = TextWrapping.NoWrap,
        };

        AddTextInlines(textBlock, text, oppositeText, kind);

        var border = new Border
        {
            Background = GetCellBackground(kind, isLeft),
            Padding = new Thickness(4, 1),
            Child = textBlock,
            MinHeight = 20,
            ClipToBounds = true,
        };
        Grid.SetColumn(border, column);
        return border;
    }

    private static void AddTextInlines(TextBlock textBlock, string? text, string? oppositeText, DiffKind kind)
    {
        if (text is null)
        {
            return;
        }

        if (kind != DiffKind.Changed || oppositeText is null)
        {
            textBlock.Inlines!.Add(new Run(text));
            return;
        }

        var diffFlags = BuildCharacterDiffFlags(text, oppositeText);
        var start = 0;
        while (start < text.Length)
        {
            var isDifferent = diffFlags[start];
            var end = start + 1;
            while (end < text.Length && diffFlags[end] == isDifferent)
            {
                end++;
            }

            textBlock.Inlines!.Add(new Run(text[start..end])
            {
                Background = isDifferent ? CharacterDiffBackground : null,
                Foreground = isDifferent ? CharacterDiffTextBrush : TextBrush,
            });
            start = end;
        }
    }

    private static IBrush GetCellBackground(DiffKind kind, bool isLeft)
    {
        return kind switch
        {
            DiffKind.Equal => EqualBackground,
            DiffKind.Changed => ChangedBackground,
            DiffKind.Inserted => isLeft ? MissingBackground : InsertedBackground,
            DiffKind.Deleted => isLeft ? DeletedBackground : MissingBackground,
            _ => EqualBackground,
        };
    }

    private static List<DiffRow> BuildLineDiff(string[] leftLines, string[] rightLines)
    {
        var lcs = BuildLcsTable(leftLines, rightLines);
        var rows = new List<DiffRow>();
        var leftIndex = 0;
        var rightIndex = 0;

        while (leftIndex < leftLines.Length || rightIndex < rightLines.Length)
        {
            if (leftIndex < leftLines.Length &&
                rightIndex < rightLines.Length &&
                leftLines[leftIndex] == rightLines[rightIndex])
            {
                rows.Add(new DiffRow(
                    leftLines[leftIndex],
                    rightLines[rightIndex],
                    leftIndex + 1,
                    rightIndex + 1,
                    DiffKind.Equal));
                leftIndex++;
                rightIndex++;
            }
            else
            {
                var deleted = new List<(string Text, int LineNumber)>();
                var inserted = new List<(string Text, int LineNumber)>();

                while (leftIndex < leftLines.Length ||
                       rightIndex < rightLines.Length)
                {
                    if (leftIndex < leftLines.Length &&
                        rightIndex < rightLines.Length &&
                        leftLines[leftIndex] == rightLines[rightIndex])
                    {
                        break;
                    }

                    if (rightIndex >= rightLines.Length ||
                        (leftIndex < leftLines.Length && lcs[leftIndex + 1, rightIndex] >= lcs[leftIndex, rightIndex + 1]))
                    {
                        deleted.Add((leftLines[leftIndex], leftIndex + 1));
                        leftIndex++;
                    }
                    else
                    {
                        inserted.Add((rightLines[rightIndex], rightIndex + 1));
                        rightIndex++;
                    }
                }

                var pairedCount = Math.Min(deleted.Count, inserted.Count);
                for (var i = 0; i < pairedCount; i++)
                {
                    rows.Add(new DiffRow(
                        deleted[i].Text,
                        inserted[i].Text,
                        deleted[i].LineNumber,
                        inserted[i].LineNumber,
                        DiffKind.Changed));
                }

                for (var i = pairedCount; i < deleted.Count; i++)
                {
                    rows.Add(new DiffRow(
                        deleted[i].Text,
                        null,
                        deleted[i].LineNumber,
                        null,
                        DiffKind.Deleted));
                }

                for (var i = pairedCount; i < inserted.Count; i++)
                {
                    rows.Add(new DiffRow(
                        null,
                        inserted[i].Text,
                        null,
                        inserted[i].LineNumber,
                        DiffKind.Inserted));
                }
            }
        }

        return rows;
    }

    private static int[,] BuildLcsTable(string[] leftLines, string[] rightLines)
    {
        var table = new int[leftLines.Length + 1, rightLines.Length + 1];
        for (var left = leftLines.Length - 1; left >= 0; left--)
        {
            for (var right = rightLines.Length - 1; right >= 0; right--)
            {
                table[left, right] = leftLines[left] == rightLines[right]
                    ? table[left + 1, right + 1] + 1
                    : Math.Max(table[left + 1, right], table[left, right + 1]);
            }
        }

        return table;
    }

    private static bool[] BuildCharacterDiffFlags(string text, string oppositeText)
    {
        var table = new int[text.Length + 1, oppositeText.Length + 1];
        for (var i = text.Length - 1; i >= 0; i--)
        {
            for (var j = oppositeText.Length - 1; j >= 0; j--)
            {
                table[i, j] = text[i] == oppositeText[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        var flags = Enumerable.Repeat(true, text.Length).ToArray();
        var left = 0;
        var right = 0;
        while (left < text.Length && right < oppositeText.Length)
        {
            if (text[left] == oppositeText[right])
            {
                flags[left] = false;
                left++;
                right++;
            }
            else if (table[left + 1, right] >= table[left, right + 1])
            {
                left++;
            }
            else
            {
                right++;
            }
        }

        return flags;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private sealed record DiffRow(
        string? LeftText,
        string? RightText,
        int? LeftLineNumber,
        int? RightLineNumber,
        DiffKind Kind);

    private enum DiffKind
    {
        Equal,
        Changed,
        Inserted,
        Deleted,
    }
}
