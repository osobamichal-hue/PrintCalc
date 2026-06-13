using System.Globalization;
using System.Windows.Data;
using PrintCalc.Core.Enums;

namespace PrintCalc.App.Converters;

public sealed class StockMovementTypeToCzechConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StockMovementType t
            ? t switch
            {
                StockMovementType.Receipt => "Příjem",
                StockMovementType.Issue => "Výdej",
                StockMovementType.InventoryAdjustment => "Inventura",
                _ => t.ToString()
            }
            : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
