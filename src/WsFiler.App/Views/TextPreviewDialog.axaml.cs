using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;

namespace WsFiler.App.Views;

public partial class TextPreviewDialog : Window
{
    private const double OwnerSizeRatio = 0.9;

    public TextPreviewDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public TextPreviewDialog(string path, string text, bool isTruncated)
        : this()
    {
        PathTextBlock.Text = isTruncated ? $"{path} (先頭のみ表示)" : path;
        PreviewTextBox.Text = text;
        Opened += (_, _) =>
        {
            PreviewTextBox.Focus();
            PreviewTextBox.CaretIndex = 0;
        };
    }

    public void FitToOwner(Window owner)
    {
        Width = Math.Max(MinWidth, owner.Bounds.Width * OwnerSizeRatio);
        Height = Math.Max(MinHeight, owner.Bounds.Height * OwnerSizeRatio);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        Close();
    }
}
