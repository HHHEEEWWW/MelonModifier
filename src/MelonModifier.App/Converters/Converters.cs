using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace MelonModifier.App.Converters;

/// <summary>bool -> Visibility（true=Visible，false=Collapsed）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
        var b = value is bool v && v;
        if (invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>null 或空字符串 -> Collapsed，否则 Visible。</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>int -> Visibility（0=Visible，非 0=Collapsed；参数 reverse 则相反）。</summary>
public sealed class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var reverse = string.Equals(parameter as string, "reverse", StringComparison.OrdinalIgnoreCase);
        var zero = value is int i && i == 0;
        var show = reverse ? !zero : zero;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool 取反（参数 true 时不做取反）。</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var noInvert = string.Equals(parameter as string, "noinvert", StringComparison.OrdinalIgnoreCase);
        var b = value is bool v && v;
        return noInvert ? b : !b;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var noInvert = string.Equals(parameter as string, "noinvert", StringComparison.OrdinalIgnoreCase);
        var b = value is bool v && v;
        return noInvert ? b : !b;
    }
}

/// <summary>bool -> 画刷（parameter 传资源 Key 列表 "trueKey,falseKey"）。</summary>
public sealed class BoolToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var keys = (parameter as string)?.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var b = value is bool v && v;
        var key = b ? (keys?.Length > 0 ? keys[0] : "Brush.Neon")
                    : (keys?.Length > 1 ? keys[1] : "Brush.TextDim");
        return Application.Current.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>StatusKind（0 未装 / 1 已装 / 2 可升级）-> 前景画刷。</summary>
public sealed class StatusKindToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            2 => "Brush.Warning",
            1 => "Brush.Success",
            _ => "Brush.TextDim",
        };
        return Application.Current.TryFindResource(key) ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
