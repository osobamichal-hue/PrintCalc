using System.Globalization;
using System.Windows;

namespace PrintCalc.App.Views;

public partial class ModelEditWindow : Window
{
    public ModelEditWindow(ModelEditModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    public ModelEditModel Model => (ModelEditModel)DataContext;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Model.Name))
            return;

        DialogResult = true;
        Close();
    }
}

public sealed class ModelEditModel
{
    public string Name { get; set; } = "";
    public string EstimatedMaterialGrams { get; set; } = "";
    public string EstimatedPrintHours { get; set; } = "";
    public string? Notes { get; set; }

    public decimal? ParseMaterial()
    {
        if (string.IsNullOrWhiteSpace(EstimatedMaterialGrams)) return null;
        return decimal.TryParse(EstimatedMaterialGrams.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public decimal? ParseHours()
    {
        if (string.IsNullOrWhiteSpace(EstimatedPrintHours)) return null;
        return decimal.TryParse(EstimatedPrintHours.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}

