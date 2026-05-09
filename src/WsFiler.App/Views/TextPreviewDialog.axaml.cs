using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System;
using System.Linq;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class TextPreviewDialog : Window
{
    private const double OwnerSizeRatio = 0.9;
    private const double LineHeight = 16;

    private ScrollViewer? scrollViewer;
    private string? alternateContent;
    private bool isHexView;
    private Action? editAction;

    public TextPreviewDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public TextPreviewDialog(
        string path,
        string text,
        bool isTruncated,
        string? alternateContent = null,
        bool isInitiallyHex = false,
        Action? editAction = null)
        : this()
    {
        Title = Strings.Dialog_Preview_Title;
        PathTextBlock.Text = isTruncated ? string.Format(Strings.Dialog_Preview_Truncated, path) : path;
        PreviewTextBox.Text = text;
        this.alternateContent = alternateContent;
        this.isHexView = isInitiallyHex;
        this.editAction = editAction;
        Opened += (_, _) =>
        {
            PreviewTextBox.Focus();
            PreviewTextBox.CaretIndex = 0;
            scrollViewer = PreviewTextBox.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        };
    }

    public void FitToOwner(Window owner)
    {
        Width = Math.Max(MinWidth, owner.Bounds.Width * OwnerSizeRatio);
        Height = Math.Max(MinHeight, owner.Bounds.Height * OwnerSizeRatio);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key == Key.Tab && alternateContent is not null)
        {
            var previous = PreviewTextBox.Text ?? string.Empty;
            PreviewTextBox.Text = alternateContent;
            alternateContent = previous;
            isHexView = !isHexView;
            PreviewTextBox.CaretIndex = 0;
            if (scrollViewer is not null)
            {
                scrollViewer.Offset = new Vector(0, 0);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.E && editAction is not null && !isHexView)
        {
            e.Handled = true;
            var action = editAction;
            Close();
            action();
            return;
        }

        var delta = e.Key switch
        {
            Key.Up => -LineHeight,
            Key.Down => LineHeight,
            _ => 0d,
        };

        if (delta == 0 || scrollViewer is null)
        {
            return;
        }

        var current = scrollViewer.Offset;
        var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var newY = Math.Clamp(current.Y + delta, 0, maxY);
        scrollViewer.Offset = new Vector(current.X, newY);
        e.Handled = true;
    }
}
