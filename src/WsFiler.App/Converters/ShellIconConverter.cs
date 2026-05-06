using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace WsFiler.App.Converters;

public sealed class ShellIconConverter : IValueConverter
{
    private const uint FileAttributeReadonly = 0x00000001;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallicon = 0x000000001;
    private const uint ShgfiUsefileattributes = 0x000000010;

    private static readonly ConcurrentDictionary<string, AvaloniaBitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || !OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return null;
        }

        var cacheKey = GetCacheKey(path);
#pragma warning disable CA1416
        return Cache.GetOrAdd(cacheKey, _ => LoadShellIcon(path));
#pragma warning restore CA1416
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;

    private static string GetCacheKey(string path)
    {
        if (Directory.Exists(path))
        {
            return "<directory>";
        }

        var extension = Path.GetExtension(path);
        return string.IsNullOrEmpty(extension) ? "<file>" : extension;
    }

    [SupportedOSPlatform("windows6.1")]
    private static AvaloniaBitmap? LoadShellIcon(string path)
    {
        var attributes = Directory.Exists(path)
            ? FileAttributeDirectory
            : FileAttributeNormal;

        var flags = ShgfiIcon | ShgfiSmallicon | ShgfiUsefileattributes;
        var info = new Shfileinfo();
        var result = SHGetFileInfo(
            path,
            attributes,
            ref info,
            (uint)Marshal.SizeOf<Shfileinfo>(),
            flags);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = Icon.FromHandle(info.hIcon);
            using var bitmap = icon.ToBitmap();
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            return new AvaloniaBitmap(stream);
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref Shfileinfo psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Shfileinfo
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
