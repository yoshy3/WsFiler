using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public delegate Task<(string Path, Bitmap Bitmap)?> ImagePreviewNavigator(int direction);

public partial class ImagePreviewDialog : Window
{
    private const double OwnerSizeRatio = 0.9;

    private ImagePreviewNavigator? navigator;
    private Bitmap? currentBitmap;

    public ImagePreviewDialog()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        Closed += (_, _) => currentBitmap?.Dispose();
    }

    public ImagePreviewDialog(string path, Bitmap bitmap, ImagePreviewNavigator? navigator = null)
        : this()
    {
        Title = Strings.Dialog_Preview_Title;
        PathTextBlock.Text = path;
        PreviewImage.Source = bitmap;
        currentBitmap = bitmap;
        this.navigator = navigator;
    }

    public static Bitmap LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        return new Bitmap(stream);
    }

    public void FitToOwner(Window owner)
    {
        Width = Math.Max(MinWidth, owner.Bounds.Width * OwnerSizeRatio);
        Height = Math.Max(MinHeight, owner.Bounds.Height * OwnerSizeRatio);
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        var direction = e.Key switch
        {
            Key.Space => 1,
            Key.Back => -1,
            _ => 0,
        };

        if (direction == 0 || navigator is null)
        {
            return;
        }

        e.Handled = true;

        try
        {
            var result = await navigator(direction);
            if (result is null)
            {
                return;
            }

            var previous = currentBitmap;
            currentBitmap = result.Value.Bitmap;
            PreviewImage.Source = currentBitmap;
            PathTextBlock.Text = result.Value.Path;
            previous?.Dispose();

            Activate();
            Focus();
        }
        catch
        {
            // Navigation failure should not crash the dialog.
        }
    }
}
