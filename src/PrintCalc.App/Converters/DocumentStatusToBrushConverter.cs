using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrintCalc.App.Converters;

public sealed class DocumentStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant() ?? "";
        if (status.Contains("accepted") || status.Contains("confirmed") || status.Contains("paid") || status.Contains("zaplac"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")!);
        if (status.Contains("sent") || status.Contains("issued") || status.Contains("odesla"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF6C00")!);
        if (status.Contains("cancel") || status.Contains("storno") || status.Contains("zru"))
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C62828")!);
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#546E7A")!);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

