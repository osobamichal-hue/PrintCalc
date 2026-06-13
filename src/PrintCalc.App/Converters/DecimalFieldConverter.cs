using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PrintCalc.App.Converters;

/// <summary>Umožní zadat desetinné číslo s čárkou i tečkou (např. 0,08 kWh/h).</summary>
public class DecimalFieldConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        culture = culture ?? CultureInfo.CurrentCulture;
        return value switch
        {
            decimal d => d.ToString("G29", culture),
            null => "",
            _ => ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        culture = culture ?? CultureInfo.CurrentCulture;
        if (value is not string s)
            return 0m;
        s = s.Trim();
        if (s.Length == 0)
            return 0m;

        if (decimal.TryParse(s, NumberStyles.Number, culture, out var d))
            return d;
        if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out d))
            return d;

        return Binding.DoNothing;
    }
}
