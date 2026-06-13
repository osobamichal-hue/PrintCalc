using System.Globalization;
using System.Windows.Data;

namespace PrintCalc.App.Converters;

public sealed class UtcToLocalDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt) return value;
        var utc = dt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            : dt.ToUniversalTime();
        return utc.ToLocalTime();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
