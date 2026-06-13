using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BKKleaner.UI;

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is false;
}

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bool → Visibility, with optional "invert" parameter.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter is "invert") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class BytesToMbConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long bytes ? $"{bytes / 1024.0 / 1024.0:0.##} MB" : "0 MB";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>True when the bound value equals the converter parameter (for radio-style selection).</summary>
public sealed class EqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}

/// <summary>Formats an interval in minutes with the localized "minutes" unit (e.g. "30 dk").</summary>
public sealed class MinutesLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var unit = Localization.Loc.Instance["units.minutes"];
        return $"{value} {unit}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Collection count → Visibility (visible when empty, for empty-state placeholders).</summary>
public sealed class EmptyToVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Maps a temperature/usage value to a heat color: normal under 80, orange 80–90,
/// red 90–100, dark red above 100. Used to color live sensor readouts.
/// </summary>
public sealed class HeatBrushConverter : IValueConverter
{
    private static readonly System.Windows.Media.SolidColorBrush Orange = Freeze(0xFB, 0x92, 0x3C);
    private static readonly System.Windows.Media.SolidColorBrush Red = Freeze(0xEF, 0x44, 0x44);
    private static readonly System.Windows.Media.SolidColorBrush DarkRed = Freeze(0xB9, 0x1C, 0x1C);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var v = value is double d ? d : value is int i ? i : 0;
        if (v >= 100) return DarkRed;
        if (v >= 90) return Red;
        if (v >= 80) return Orange;
        return Application.Current?.TryFindResource("Brush.Text") as System.Windows.Media.Brush
               ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static System.Windows.Media.SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}

/// <summary>Formats a large raw score into a compact, readable magnitude (1.2K, 25.4M, 3.1B).</summary>
public sealed class ScoreLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var n = value is double d ? d : value is float f ? f : 0;
        var inv = CultureInfo.InvariantCulture;
        return n switch
        {
            >= 1_000_000_000 => (n / 1_000_000_000).ToString("0.0", inv) + "B",
            >= 1_000_000 => (n / 1_000_000).ToString("0.0", inv) + "M",
            >= 1_000 => (n / 1_000).ToString("0.0", inv) + "K",
            _ => n.ToString("0", inv)
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Localizes an enum value via a "{prefix}.{value}" localization key.</summary>
public sealed class EnumLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var prefix = parameter?.ToString() ?? "enum";
        return Localization.Loc.Instance[$"{prefix}.{value}".ToLowerInvariant()];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
