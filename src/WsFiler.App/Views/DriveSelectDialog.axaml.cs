using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WsFiler.Presentation.Resources;

namespace WsFiler.App.Views;

public partial class DriveSelectDialog : Window
{
    private readonly List<DriveInfo> drives;

    public DriveSelectDialog()
        : this(null)
    {
    }

    public DriveSelectDialog(string? currentPath)
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        Title = Strings.Dialog_Drive_Title;
        OkButton.Content = Strings.Dialog_Common_Ok;
        CancelButton.Content = Strings.Dialog_Common_Cancel;

        drives = OperatingSystem.IsLinux()
            ? GetLinuxCommonDirectories()
            : OperatingSystem.IsMacOS()
                ? GetMacCommonDirectories()
                : DriveInfo.GetDrives().Where(d => d.IsReady).ToList();

        DriveListBox.ItemsSource = (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            ? drives.Select(d => d.Name.TrimEnd(Path.DirectorySeparatorChar)).Select(p => string.IsNullOrEmpty(p) ? "/" : p).ToList()
            : drives.Select(d => $"{d.Name.TrimEnd(Path.DirectorySeparatorChar)} ({d.DriveType})").ToList();

        if (drives.Count > 0)
        {
            DriveListBox.SelectedIndex = GetInitialDriveIndex(currentPath);
        }

        PathInput.Text = currentPath ?? string.Empty;

        Opened += (_, _) =>
        {
            PathInput.Focus();
            PathInput.SelectAll();
        };
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => CloseWithSelection();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void OnDoubleTapped(object? sender, TappedEventArgs e) => CloseWithSelection();

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && (e.KeyModifiers & KeyModifiers.Shift) == 0 && !PathInput.IsFocused)
        {
            e.Handled = true;
            PathInput.Focus();
            PathInput.SelectAll();
            return;
        }

        if (PathInput.IsFocused)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CloseWithSelection();
        }
        else if (e.Key is Key.Up or Key.Down)
        {
            e.Handled = true;
            MoveSelection(e.Key == Key.Down ? 1 : -1);
        }
        else if (TryGetDriveLetter(e.Key, out var driveLetter))
        {
            e.Handled = true;
            SelectDriveByLetter(driveLetter);
        }
    }

    private void OnPathInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var path = PathInput.Text?.Trim();
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (TryResolveDriveLetterShortcut(path, out var driveTarget))
            {
                Close(driveTarget);
                return;
            }

            Close(path);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }

    private static bool TryResolveDriveLetterShortcut(string input, out string resolved)
    {
        resolved = string.Empty;
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var trimmed = input.TrimEnd(':');
        if (trimmed.Length != 1 || !char.IsLetter(trimmed[0]))
        {
            return false;
        }

        try
        {
            resolved = Path.GetFullPath(trimmed + ":");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void MoveSelection(int offset)
    {
        if (drives.Count == 0)
        {
            return;
        }

        var currentIndex = DriveListBox.SelectedIndex >= 0 ? DriveListBox.SelectedIndex : 0;
        var nextIndex = Math.Clamp(currentIndex + offset, 0, drives.Count - 1);
        DriveListBox.SelectedIndex = nextIndex;
        if (DriveListBox.SelectedItem is { } selectedItem)
        {
            DriveListBox.ScrollIntoView(selectedItem);
        }
    }

    private void SelectDriveByLetter(char driveLetter)
    {
        var rootPrefix = $"{driveLetter}:";
        var index = drives.FindIndex(d => d.Name.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return;
        }

        DriveListBox.SelectedIndex = index;
        if (DriveListBox.SelectedItem is { } selectedItem)
        {
            DriveListBox.ScrollIntoView(selectedItem);
        }
    }

    private static bool TryGetDriveLetter(Key key, out char driveLetter)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            driveLetter = (char)('A' + key - Key.A);
            return true;
        }

        driveLetter = '\0';
        return false;
    }

    private static List<DriveInfo> GetLinuxCommonDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new[] { home, "/", "/etc", "/usr", "/var", "/mnt", "/opt" };
        return paths.Where(Directory.Exists).Select(p => new DriveInfo(p)).ToList();
    }

    private static List<DriveInfo> GetMacCommonDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new[] { home, "/", "/Applications", "/Users", "/opt", "/usr" };
        var result = paths.Where(Directory.Exists).Select(p => new DriveInfo(p)).ToList();

        if (Directory.Exists("/Volumes"))
        {
            var volumes = Directory.GetDirectories("/Volumes")
                .Where(Directory.Exists)
                .Select(p => new DriveInfo(p));
            result.AddRange(volumes);
        }

        return result;
    }

    private int GetInitialDriveIndex(string? currentPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return 0;
        }

        var currentRoot = Path.GetPathRoot(currentPath);
        if (string.IsNullOrEmpty(currentRoot))
        {
            return 0;
        }

        var index = drives.FindIndex(d => string.Equals(
            d.RootDirectory.FullName,
            currentRoot,
            StringComparison.OrdinalIgnoreCase));

        return index >= 0 ? index : 0;
    }

    private void CloseWithSelection()
    {
        if (DriveListBox.SelectedIndex < 0)
        {
            return;
        }

        if (DriveListBox.SelectedIndex < drives.Count)
        {
            Close(drives[DriveListBox.SelectedIndex].RootDirectory.FullName);
        }
    }
}
