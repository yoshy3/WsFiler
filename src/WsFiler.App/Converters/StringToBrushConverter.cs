using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace WsFiler.App.Converters;

/// <summary>
/// Converts a color string (e.g. "#f4f4f4", "Transparent") to an <see cref="IBrush"/>.
/// Used instead of Avalonia's implicit string→IBrush conversion, which relies on
/// reflection-based TypeConverter discovery that is trimmed away under NativeAOT.
/// </summary>
public sealed class StringToBrushConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, IBrush?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string text || text.Length == 0)
        {
            return null;
        }

        return Cache.GetOrAdd(text, static t =>
        {
            try
            {
                return Brush.Parse(t);
            }
            catch (FormatException)
            {
                return null;
            }
        });
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
