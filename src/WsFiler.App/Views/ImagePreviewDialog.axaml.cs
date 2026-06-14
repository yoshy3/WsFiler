using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
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

    private double scale = 1.0;
    private double tx = 0.0;
    private double ty = 0.0;
    private bool isZoomedOrPanned;

    private Point panStartMouse;
    private double panStartTx;
    private double panStartTy;
    private bool isPanning;

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
        return LoadBitmap(stream);
    }

    public static Bitmap LoadBitmap(Stream stream)
    {
        return new Bitmap(stream);
    }

    public void FitToOwner(Window owner)
    {
        Width = Math.Max(MinWidth, owner.Bounds.Width * OwnerSizeRatio);
        Height = Math.Max(MinHeight, owner.Bounds.Height * OwnerSizeRatio);
    }

    private void ResetView()
    {
        if (currentBitmap == null) return;

        double viewportWidth = ImageContainer.Bounds.Width;
        double viewportHeight = ImageContainer.Bounds.Height;

        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        double imageWidth = currentBitmap.Size.Width;
        double imageHeight = currentBitmap.Size.Height;

        double scaleX = viewportWidth / imageWidth;
        double scaleY = viewportHeight / imageHeight;
        scale = Math.Min(scaleX, scaleY);

        tx = (viewportWidth - imageWidth * scale) / 2.0;
        ty = (viewportHeight - imageHeight * scale) / 2.0;

        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (PreviewImage == null) return;

        var matrix = new Matrix(scale, 0, 0, scale, tx, ty);
        if (PreviewImage.RenderTransform is MatrixTransform mt)
        {
            mt.Matrix = matrix;
        }
        else
        {
            PreviewImage.RenderTransform = new MatrixTransform(matrix);
        }
    }

    private void OnImageContainerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (!isZoomedOrPanned)
        {
            ResetView();
        }
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            Point mousePos = e.GetPosition(ImageContainer);

            ZoomAroundPoint(zoomFactor, mousePos);
            e.Handled = true;
        }
    }

    private void ZoomAroundPoint(double factor, Point mousePos)
    {
        double newScale = scale * factor;
        if (newScale < 0.05 || newScale > 50.0)
        {
            return;
        }

        tx = mousePos.X - factor * (mousePos.X - tx);
        ty = mousePos.Y - factor * (mousePos.Y - ty);
        scale = newScale;
        isZoomedOrPanned = true;

        UpdateTransform();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pointer = e.GetCurrentPoint(ImageContainer);
        if (pointer.Properties.IsLeftButtonPressed)
        {
            panStartMouse = e.GetPosition(ImageContainer);
            panStartTx = tx;
            panStartTy = ty;
            isPanning = true;
            ImageContainer.Cursor = new Cursor(StandardCursorType.Hand);
            e.Handled = true;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (isPanning)
        {
            var pointer = e.GetCurrentPoint(ImageContainer);
            if (pointer.Properties.IsLeftButtonPressed)
            {
                var currentMouse = e.GetPosition(ImageContainer);
                double dx = currentMouse.X - panStartMouse.X;
                double dy = currentMouse.Y - panStartMouse.Y;

                tx = panStartTx + dx;
                ty = panStartTy + dy;
                isZoomedOrPanned = true;
                UpdateTransform();
                e.Handled = true;
            }
            else
            {
                isPanning = false;
                ImageContainer.Cursor = null;
            }
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (isPanning)
        {
            isPanning = false;
            ImageContainer.Cursor = null;
            e.Handled = true;
        }
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

            isZoomedOrPanned = false;
            ResetView();

            Activate();
            Focus();
        }
        catch
        {
            // Navigation failure should not crash the dialog.
        }
    }
}
