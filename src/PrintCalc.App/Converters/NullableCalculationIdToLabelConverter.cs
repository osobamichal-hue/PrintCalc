using System.Globalization;
using System.Windows.Data;

namespace PrintCalc.App.Converters;

/// <summary>Zobrazí #id kalkulace nebo pomlčku pro ruční řádek.</summary>
public sealed class NullableCalculationIdToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int id ? $"#{id}" : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
