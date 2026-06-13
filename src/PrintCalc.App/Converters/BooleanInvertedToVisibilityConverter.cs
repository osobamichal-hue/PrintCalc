using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PrintCalc.App.Converters;

public sealed class BooleanInvertedToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is bool b && !b;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

