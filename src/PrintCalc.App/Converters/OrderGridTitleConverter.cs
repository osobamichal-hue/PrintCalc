using System.Globalization;
using System.Windows.Data;
using PrintCalc.Core.Helpers;
using PrintCalc.Core.Entities;

namespace PrintCalc.App.Converters;

/// <summary>Celý řádek zakázky → srozumitelný název v mřížce (výcuc z položek u generických titulů).</summary>
public sealed class OrderGridTitleConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Order o ? DocumentTitleExcerpt.ForOrderGridCaption(o) : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
