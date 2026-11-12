using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace BetterFences.Views;

/// <summary>Visible si le nombre ≥ paramètre (par défaut 1).</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int min = parameter != null && int.TryParse(parameter.ToString(), out var m) ? m : 1;
        bool invert = min < 0;
        int count = value is int c ? c : 0;
        bool visible = invert ? count < -min : count >= min;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (value is true) ^ Inverse ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
